﻿namespace McNNTP.Common
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.RegularExpressions;

    /// <summary>
    /// A utility class which provides implementations of portions of RFC 5322 and 5234.
    /// </summary>
    public static class InternetMessageFormatUtility
    {
        /// <summary>
        /// A-Z / a-z.
        /// </summary>
        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_CHAR_ALPHA = @"\x41-\x5a\x61-\x7a";

        /// <summary>
        /// Any 7-bit US-ASCII character, excluding NUL.
        /// </summary>
        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_CHAR_CHAR = @"\x01-\x7f";

        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_CHAR_CR = @"\x0d";

        /// <summary>
        /// Internet standard newline.
        /// </summary>
        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_CHAR_CRLF = REGEX_CHAR_CR + REGEX_CHAR_LF;

        /// <summary>
        /// Controls.
        /// </summary>
        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_CHAR_CTL = @"\x00-\x1f\x7f";

        /// <summary>
        /// 0-9.
        /// </summary>
        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_CHAR_DIGIT = @"\x30-x39";

        /// <summary>
        /// Double quote.
        /// </summary>
        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_CHAR_DQUOTE = @"\x22";

        /// <summary>
        /// Hexadecimal digits.
        /// </summary>
        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_CHAR_HEXDIG = REGEX_CHAR_DIGIT + "ABCDEF";

        /// <summary>
        /// Horizontal tab.
        /// </summary>
        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_CHAR_HTAB = @"\x09";

        /// <summary>
        /// Line feed.
        /// </summary>
        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_CHAR_LF = @"\x0a";

        /// <summary>
        /// 8 bits of data.
        /// </summary>
        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_CHAR_OCTET = @"\x00-\xff";

        /// <summary>
        /// Space.
        /// </summary>
        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_CHAR_SP = @"\x20";

        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_CHAR_WSP = REGEX_CHAR_SP + REGEX_CHAR_HTAB;

        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_CHAR_VCHAR = @"\x21-\x7e";

        /// <summary>
        /// Printable US-ASCII characters not include "(", ")", or "\".
        /// </summary>
        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_CHAR_CTEXT = @"\x21-\x27\x2a-\x5b\x5d-x7e";

        private const string REGEX_PATTERN_ATEXT = @"[A-Za-z0-9!#$%&'*+-/=?^_`{|}~]";

        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_PATTERN_ATOM =
            @"(" + REGEX_PATTERN_CFWS + ")?" + REGEX_PATTERN_ATEXT + "+(" + REGEX_PATTERN_CFWS + ")?";

        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_PATTERN_CFWS =
            @"((((" + REGEX_PATTERN_FWS + ")?(" + REGEX_PATTERN_COMMENT + "))+(" + REGEX_PATTERN_FWS + ")?)|("
            + REGEX_PATTERN_FWS + "))";

        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_PATTERN_COMMENT =
            @"\(((" + REGEX_PATTERN_FWS + ")?(" + REGEX_PATTERN_CCONTENT + "))*(" + REGEX_PATTERN_FWS + @")?\)";

        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_PATTERN_CCONTENT =
            @"([" + REGEX_CHAR_CTEXT + "]+|(" + REGEX_PATTERN_QUOTED_PAIR + "))";

        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_PATTERN_DOT_ATOM_TEXT = REGEX_PATTERN_ATEXT + @"+(\." + REGEX_PATTERN_ATEXT + @"+)*";

        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_PATTERN_DOT_ATOM =
            @"(" + REGEX_PATTERN_CFWS + ")?(" + REGEX_PATTERN_DOT_ATOM_TEXT + ")(" + REGEX_PATTERN_CFWS + ")?";

        /// <summary>
        /// FOLDING WHITESPACE.
        /// </summary>
        private const string REGEX_PATTERN_FWS =
            "([" + REGEX_CHAR_WSP + "]*(" + REGEX_CHAR_CRLF + "))?[" + REGEX_CHAR_WSP + "]+";

        /// <summary>
        /// Use of this linear-white-space rule permits lines containing only white
        /// space that are no longer legal in mail headers and have caused interoperability problems in other contexts.
        /// </summary>
        /// <remarks>
        /// Do not use when defining mail headers and use with caution in other contexts.
        /// </remarks>
        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_PATTERN_LWSP = "([" + REGEX_CHAR_WSP + "]|" + REGEX_CHAR_CRLF + REGEX_CHAR_WSP + ")*";

        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string REGEX_PATTERN_QUOTED_PAIR = @"\\([" + REGEX_CHAR_VCHAR + "]|[" + REGEX_CHAR_WSP + "])";

        public static bool IsAText(this string val)
        {
            // See RFC 5322 3.2.3
            return Regex.IsMatch(val, REGEX_PATTERN_ATEXT);
        }

        public static bool IsAtom(this string val)
        {
            // See RFC 5322 3.2.3
            return Regex.IsMatch(val, REGEX_PATTERN_ATOM);
        }

        public static bool IsUsenetMessageId(this string val)
        {
            // See RFC 5536 3.1.3
            const string mdText = "\x21-\x3d\x3f-\x5a\x5e-\x7e";
            const string noFoldLiteral = @"\[[" + mdText + @"]*\]";
            const string idLeft = REGEX_PATTERN_DOT_ATOM_TEXT;
            const string idRight = "((" + REGEX_PATTERN_DOT_ATOM_TEXT + ")|(" + noFoldLiteral + "))";
            const string msgCore = idLeft + "@" + idRight;
            const string msgId = "<" + msgCore + ">";

            return Regex.IsMatch(val, msgId);
        }
    }
}
