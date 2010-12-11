﻿#light

namespace Vim

/// Thin wrapper around System.String which takes a specific comparer value
/// Useful for creating a case insensitive string key for a Map
type ComparableString ( _value : string, _comparer : System.StringComparer ) = 

    member x.Value = _value
                    
    override x.GetHashCode() = _value.GetHashCode()
    override x.Equals(obj) =
        match obj with
        | :? ComparableString as other -> _comparer.Equals(_value, other.Value)
        | _ -> false

    override x.ToString() = _value

    interface System.IComparable with
        member x.CompareTo yObj =
            match yObj with
            | :? ComparableString as other -> _comparer.Compare(_value, other.Value)
            | _ -> invalidArg "yObj" "Cannot compare values of different types"  

    interface System.IEquatable<KeyInput> with
        member x.Equals other = _comparer.Equals(_value, other)

    static member CreateOrdinalIgnoreCase value = ComparableString(value, System.StringComparer.OrdinalIgnoreCase)

/// Utility class for converting Key notations into KeyInput and vice versa
/// :help key-notation for all of the key codes
module KeyNotationUtil =

    let ManualKeyList = 
        [
            ("<Nul>",KeyInputUtil.VimKeyAndModifiersToKeyInput VimKey.AtSign KeyModifiers.Control)
            ("<Bs>", KeyInputUtil.VimKeyToKeyInput VimKey.Back)
            ("<Tab>",KeyInputUtil.VimKeyToKeyInput VimKey.Tab)
            ("<NL>", KeyInputUtil.VimKeyToKeyInput VimKey.Enter)
            ("<FF>", KeyInputUtil.CharWithControlToKeyInput 'l')
            ("<CR>", KeyInputUtil.VimKeyToKeyInput VimKey.Enter)
            ("<Return>", KeyInputUtil.VimKeyToKeyInput VimKey.Enter)
            ("<Enter>", KeyInputUtil.VimKeyToKeyInput VimKey.Enter)
            ("<Esc>", KeyInputUtil.VimKeyToKeyInput VimKey.Escape)
            ("<Space>", KeyInputUtil.CharToKeyInput ' ')
            ("<lt>", KeyInputUtil.CharToKeyInput '<')
            ("<Bslash>", KeyInputUtil.CharToKeyInput '\\' )
            ("<Bar>", KeyInputUtil.CharToKeyInput '|')
            ("<Del>", KeyInputUtil.VimKeyToKeyInput VimKey.Delete)
            ("<Up>", KeyInputUtil.VimKeyToKeyInput VimKey.Up)
            ("<Down>", KeyInputUtil.VimKeyToKeyInput VimKey.Down)
            ("<Left>", KeyInputUtil.VimKeyToKeyInput VimKey.Left)
            ("<Right>", KeyInputUtil.VimKeyToKeyInput VimKey.Right)
            ("<Help>", KeyInputUtil.VimKeyToKeyInput VimKey.Help)
            ("<Insert>", KeyInputUtil.VimKeyToKeyInput VimKey.Insert)
            ("<Home>", KeyInputUtil.VimKeyToKeyInput VimKey.Home)
            ("<kHome>", KeyInputUtil.VimKeyToKeyInput VimKey.Home)
            ("<End>", KeyInputUtil.VimKeyToKeyInput VimKey.End)
            ("<PageUp>", KeyInputUtil.VimKeyToKeyInput VimKey.PageUp)
            ("<PageDown>", KeyInputUtil.VimKeyToKeyInput VimKey.PageDown)
            ("<kHome>", KeyInputUtil.VimKeyToKeyInput VimKey.Home)
            ("<kEnd>", KeyInputUtil.VimKeyToKeyInput VimKey.End)
            ("<kPageUp>", KeyInputUtil.VimKeyToKeyInput VimKey.PageUp)
            ("<kPageDown>", KeyInputUtil.VimKeyToKeyInput VimKey.PageDown)
            ("<kMultiply>", KeyInputUtil.VimKeyToKeyInput VimKey.KeypadMultiply)
            ("<kPlus>", KeyInputUtil.VimKeyToKeyInput VimKey.KeypadPlus)
            ("<kDivide>", KeyInputUtil.VimKeyToKeyInput VimKey.KeypadDivide)
            ("<kPoint>", KeyInputUtil.VimKeyToKeyInput VimKey.KeypadDecimal)
            ("<kPlus>", KeyInputUtil.VimKeyToKeyInput VimKey.KeypadPlus)
            ("<k0>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad0)
            ("<k1>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad1)
            ("<k2>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad2)
            ("<k3>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad3)
            ("<k4>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad4)
            ("<k5>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad5)
            ("<k6>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad6)
            ("<k7>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad7)
            ("<k8>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad8)
            ("<k9>", KeyInputUtil.VimKeyToKeyInput VimKey.Keypad9)
        ]

    let FunctionKeys = 
        [VimKey.F1;VimKey.F2;VimKey.F3;VimKey.F4;VimKey.F5;VimKey.F6;VimKey.F7;VimKey.F8;VimKey.F9;VimKey.F10;VimKey.F11;VimKey.F12]
            |> Seq.mapi (fun i k -> (i+1),k)
            |> Seq.map (fun (number,key) -> (sprintf "<F%d>" number),KeyInputUtil.VimKeyToKeyInput key)
            |> List.ofSeq

    /// Map of the special key names to their KeyInput representation.  Does not include the <> bookends
    /// Not supported
    /// <CSI>		command sequence intro  ALT-Esc 155	*<CSI>*
    /// <xCSI>		CSI when typed in the GUI		*<xCSI>*
    /// <Undo>		undo key
    /// <EOL>
    /// <kEnter>	keypad Enter			*keypad-enter*
    let SpecialKeyMap = 
        ManualKeyList
        |> Seq.append FunctionKeys
        |> Seq.map (fun (k,v) -> k.Substring(1,k.Length-2),v)
        |> Seq.map (fun (k,v) -> (ComparableString.CreateOrdinalIgnoreCase k),v)
        |> Map.ofSeq

    /// Break up a string into a set of key notation entries
    let SplitIntoKeyNotationEntries (data:string) =

        let rec inner (rest:char list) withData =

            // Check and see if a list starts with a given prefix 
            let rec startsWith prefix target =
                match ListUtil.tryHead prefix, ListUtil.tryHead target with
                | Some(prefixHead,prefixTail),Some(targetHead,targetTail) ->
                    if CharUtil.IsEqualIgnoreCase prefixHead targetHead then startsWith prefixTail targetTail
                    else false
                | None,_ -> true
                | Some(_),None -> false

            // Just finishes the continuation.  Consider throwing here in the future
            let error() = 
                let str = rest |> StringUtil.ofCharList
                withData [str]

            if startsWith ['<'] rest then 
                match rest |> List.tryFindIndex (fun c -> c = '>') with
                | None -> error()
                | Some(index) ->
                    let length = index+1
                    let str = rest |> Seq.take length |> StringUtil.ofCharSeq
                    let rest = rest |> ListUtil.skip length
                    inner rest (fun next -> withData (str :: next))
            else 
                match ListUtil.tryHead rest with
                | None -> withData []
                | Some(h,t) -> 
                    let str = h |> StringUtil.ofChar
                    inner t (fun next -> withData (str :: next))

        inner (data |> List.ofSeq) (fun all -> all)

    let TryStringToKeyInput (data:string) = 

        // Convert the string into a keyinput 
        let convertToRaw data =
            let comparableData = ComparableString.CreateOrdinalIgnoreCase data
            match Map.tryFind comparableData SpecialKeyMap with
            | Some(ki) -> Some ki
            | None -> 
                if StringUtil.length data = 1 then KeyInputUtil.CharToKeyInput data.[0] |> Some
                else None

        // Convert and then apply the modifier
        let convertAndApply data modifier = 
            let ki = convertToRaw data 
            match modifier,ki with 
            | Some(modifier),Some(ki) -> 
                if modifier = KeyModifiers.Shift && CharUtil.IsLetter ki.Char then
                    if CharUtil.IsLower ki.Char then
                        // The shift modifier should promote a letter into the upper form 
                        ki.Char |> CharUtil.ToUpper |> KeyInputUtil.CharToKeyInput |> Some
                    else
                        // Ignore the shift modifier on an upper letter
                        Some ki
                else
                    KeyInputUtil.ChangeKeyModifiers ki modifier |> Some
            | _ -> None

        // Inside the <
        let insideLessThanGreaterThan() = 
            if data.Length >= 3 && data.[2] = '-' then
                let modifier = 
                    match data.[1] |> CharUtil.ToLower with
                    | 'c' -> KeyModifiers.Control |> Some
                    | 's' -> KeyModifiers.Shift |> Some
                    | 'a' -> KeyModifiers.Alt |> Some
                    | _ -> None
                convertAndApply (data.Substring(3, data.Length-4)) modifier
            else 
                convertToRaw (data.Substring(1,data.Length - 2))

        match StringUtil.charAtOption 0 data with
        | None -> None
        | Some('<') -> 
            if String.length data = 1 then VimKey.LessThan |> KeyInputUtil.VimKeyToKeyInput |> Some
            elif StringUtil.last data <> '>' then None
            else insideLessThanGreaterThan()
        | Some(c) -> 
            if data.Length = 1 then KeyInputUtil.CharToKeyInput data.[0] |> Some
            else None

    let StringToKeyInput data = 
        match TryStringToKeyInput data with 
        | Some(ki) -> ki
        | None -> invalidArg "data" (Resources.KeyNotationUtil_InvalidNotation data)

    let TryStringToKeyInputSet data = 
        match data |> SplitIntoKeyNotationEntries |> List.map TryStringToKeyInput |> SeqUtil.allOrNone with
        | Some(list) -> list |> KeyInputSetUtil.ofList |> Some
        | None -> None

    /// Convert tho String to a KeyInputSet 
    let StringToKeyInputSet data = 
        data
        |> SplitIntoKeyNotationEntries
        |> Seq.map StringToKeyInput
        |> KeyInputSetUtil.ofSeq
