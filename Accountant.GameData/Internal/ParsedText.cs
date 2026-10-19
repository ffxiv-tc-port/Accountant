using System;
using Dalamud.Game.Text.SeStringHandling;

namespace Accountant.Internal;

// 記住上一次解析的原始位元組與結果。每次呼叫照樣把同一段記憶體讀出來比，位元組完全相同才沿用字串
// （解析結果只由位元組決定），所以不會回舊值；解析擲例外時不更新。
// 不是執行緒安全的：一個實例只給一條執行緒用。
public sealed class ParsedText
{
    private byte[] _bytes  = [];
    private int    _length = -1;
    private string _text   = string.Empty;

    public string Get(ReadOnlySpan<byte> data)
    {
        var end = data.IndexOf((byte)0);
        if (end >= 0)
            data = data[..end];
        if (data.Length == _length && data.SequenceEqual(_bytes.AsSpan(0, _length)))
            return _text;

        var text = SeString.Parse(data).TextValue;
        if (_bytes.Length < data.Length)
            _bytes = new byte[data.Length];
        data.CopyTo(_bytes);
        _length = data.Length;
        _text   = text;
        return text;
    }
}
