namespace McNNTP.Core.Server.IRC
{
    using System;
    using System.Diagnostics;

    internal static class IrcUtility
    {
        public static string TrimRight(string trimString, int amount)
        {
            if (trimString.Length > amount)
                return trimString.Substring(0, trimString.Length - amount);

            return string.Empty;
        }

        public static string ParamGet(string Glob, int Selector, string Delimiter, bool InclusiveAfter, int StartPos)
        {
            Debug.Assert(Selector > 0);
            Debug.Assert(StartPos > 0);

            if (Delimiter.Length == 0)
            {
                if (InclusiveAfter)
                    return Glob.Substring(0, Math.Max(Glob.Length - StartPos + 1, 0));

                return Glob.Substring(StartPos, 1);
            }

            // StartPos
            var ret = TrimRight(Glob, StartPos - 1);
            if (!ret.StartsWith(Delimiter))
                ret = Delimiter + ret;
            if (!ret.EndsWith(Delimiter))
                ret = ret + Delimiter;

            int LastDlim = 1, NextDlim = 1;
            for (var repeat = 1; repeat <= Selector; repeat++)
            {
                LastDlim = NextDlim;
                NextDlim = Math.Max(NextDlim, ret.IndexOf(Delimiter, LastDlim + Delimiter.Length, StringComparison.Ordinal));
            }

            if (InclusiveAfter)
                return ret.Substring(LastDlim + Delimiter.Length, Math.Max(ret.Length - (LastDlim + Delimiter.Length) - Delimiter.Length + 1, 0));

            return ret.Substring(LastDlim + Delimiter.Length, Math.Max(NextDlim - LastDlim - Delimiter.Length, 0));
        }
    }
}
