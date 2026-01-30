using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using Android.Widget;
using Java.IO;
using Java.Lang;

namespace SimpleBluetoothTerminalNet;

internal static class TextUtil
{
    public static Color CaretBackground = new Color(unchecked((int)0xff666666));

    public const string NewlineCrlf = "\r\n";
    public const string NewlineLf = "\n";

    public static byte[] FromHexString(string s)
    {
        var buf = new ByteArrayOutputStream();
        byte b = 0;
        int nibble = 0;
        for (int pos = 0; pos < s.Length; pos++)
        {
            if (nibble == 2)
            {
                buf.Write(b);
                nibble = 0;
                b = 0;
            }
            char c = s[pos];
            if (c >= '0' && c <= '9') { nibble++; b *= 16; b += (byte)(c - '0'); }
            if (c >= 'A' && c <= 'F') { nibble++; b *= 16; b += (byte)(c - 'A' + 10); }
            if (c >= 'a' && c <= 'f') { nibble++; b *= 16; b += (byte)(c - 'a' + 10); }
        }
        if (nibble > 0)
            buf.Write(b);
        return buf.ToByteArray() ?? Array.Empty<byte>();
    }

    public static string ToHexString(byte[] buf)
    {
        return ToHexString(buf, 0, buf.Length);
    }

    public static string ToHexString(byte[] buf, int begin, int end)
    {
        var sb = new System.Text.StringBuilder(3 * (end - begin));
        ToHexString(sb, buf, begin, end);
        return sb.ToString();
    }

    public static void ToHexString(System.Text.StringBuilder sb, byte[] buf)
    {
        ToHexString(sb, buf, 0, buf.Length);
    }

    public static void ToHexString(System.Text.StringBuilder sb, byte[] buf, int begin, int end)
    {
        for (int pos = begin; pos < end; pos++)
        {
            if (sb.Length > 0)
                sb.Append(' ');
            int c;
            c = (buf[pos] & 0xff) / 16;
            if (c >= 10) c += 'A' - 10;
            else c += '0';
            sb.Append((char)c);
            c = (buf[pos] & 0xff) % 16;
            if (c >= 10) c += 'A' - 10;
            else c += '0';
            sb.Append((char)c);
        }
    }

    /// <summary>
    /// use https://en.wikipedia.org/wiki/Caret_notation to avoid invisible control characters
    /// </summary>
    public static ICharSequence ToCaretString(ICharSequence s, bool keepNewline)
    {
        return ToCaretString(s, keepNewline, s.Length());
    }

    public static ICharSequence ToCaretString(ICharSequence s, bool keepNewline, int length)
    {
        bool found = false;
        for (int pos = 0; pos < length; pos++)
        {
            if (s.CharAt(pos) < 32 && (!keepNewline || s.CharAt(pos) != '\n'))
            {
                found = true;
                break;
            }
        }
        if (!found)
            return s;
        var sb = new SpannableStringBuilder();
        for (int pos = 0; pos < length; pos++)
        {
            if (s.CharAt(pos) < 32 && (!keepNewline || s.CharAt(pos) != '\n'))
            {
                sb.Append('^');
                sb.Append((char)(s.CharAt(pos) + 64));
                sb.SetSpan(new BackgroundColorSpan(CaretBackground), sb.Length() - 2, sb.Length(), SpanTypes.ExclusiveExclusive);
            }
            else
            {
                sb.Append(s.CharAt(pos));
            }
        }
        return sb;
    }

    public class HexWatcher : Java.Lang.Object, ITextWatcher
    {
        private readonly TextView _view;
        private readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder();
        private bool _self = false;
        private bool _enabled = false;

        public HexWatcher(TextView view)
        {
            _view = view;
        }

        public void Enable(bool enable)
        {
            if (enable)
            {
                _view.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationVisiblePassword;
            }
            else
            {
                _view.InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextFlagNoSuggestions;
            }
            _enabled = enable;
        }

        public void BeforeTextChanged(ICharSequence? s, int start, int count, int after)
        {
        }

        public void OnTextChanged(ICharSequence? s, int start, int before, int count)
        {
        }

        public void AfterTextChanged(IEditable? s)
        {
            if (!_enabled || _self || s == null)
                return;

            _sb.Clear();
            for (int i = 0; i < s.Length(); i++)
            {
                char c = s.CharAt(i);
                if (c >= '0' && c <= '9') _sb.Append(c);
                if (c >= 'A' && c <= 'F') _sb.Append(c);
                if (c >= 'a' && c <= 'f') _sb.Append((char)(c + 'A' - 'a'));
            }
            for (int i = 2; i < _sb.Length; i += 3)
                _sb.Insert(i, ' ');
            string s2 = _sb.ToString();

            if (!s2.Equals(s.ToString()))
            {
                _self = true;
                s.Replace(0, s.Length(), s2);
                _self = false;
            }
        }
    }
}
