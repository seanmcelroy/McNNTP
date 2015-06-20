namespace McNNTP.Core.Server.IRC
{
    /// <summary>
    /// Reply messages - COMPLETELY compliant with RFC 2812
    /// </summary>
    public static class CommandCode
    {
        public const string RPL_WELCOME = "001"; // IRC 2.8.21

        public const string RPL_YOURHOST = "002"; // IRC 2.8.21

        public const string RPL_CREATED = "003"; // IRC 2.8.21

        public const string RPL_MYINFO = "004"; // IRC 2.8.21

        public const string RPL_BOUNCE = "005"; // Unimplemented

        public const string RPL_TRACELINK = "200"; // Unimplemented

        public const string RPL_TRACECONNECTING = "201"; // Unimplemented

        public const string RPL_TRACEHANDSHAKE = "202"; // Unimplemented

        public const string RPL_TRACEUNKNOWN = "203"; // Unimplemented

        public const string RPL_TRACEOPERATOR = "204"; // Unimplemented

        public const string RPL_TRACEUSER = "205"; // Unimplemented

        public const string RPL_TRACESERVER = "206"; // Unimplemented

        public const string RPL_TRACESERVICE = "207"; // Unimplemented

        public const string RPL_TRACENEWTYPE = "208"; // Unimplemented

        public const string RPL_TRACECLASS = "209"; // Unimplemented

        public const string RPL_TRACERECONNECT = "210"; // Unusued in RFC

        public const string RPL_TRACELOG = "261"; // Unimplemented

        public const string RPL_TRACEEND = "262"; // Unimplemented

        public const string RPL_STATSLINKINFO = "211";

        public const string RPL_STATSCOMMANDS = "212";

        public const string RPL_STATSCLINE = "213"; // RESERVED by RFC 2812

        // RPLSTATSNLINE = "214";      // RESERVED by RFC 2812
        public const string RPL_STATSILINE = "215"; // RESERVED by RFC 2812

        public const string RPL_STATSKLINE = "216"; // RESERVED by RFC 2812

        // RPLSTATSQLINE = "217";      // RESERVED by RFC 2812
        public const string RPL_STATSYLINE = "218"; // RESERVED by RFC 2812

        // RPLSERVICEINFO = "231";     // RESERVED by RFC 2812
        // RPLENDOFSERVICES = "232";   // RESERVED by RFC 2812
        // RPLSERVICE = "233";         // RESERVED by RFC 2812
        // RPLSTATSVLINE = "240";      // RESERVED by RFC 2812
        // RPLSTATSLLINE = "241";      // RESERVED by RFC 2812
        public const string RPL_STATSHLINE = "244"; // RESERVED by RFC 2812

        // RPLSTATSSLINE  = "244";     // RESERVED by RFC 2812
        // RPLSTATSPING  = "246";      // RESERVED by RFC 2812
        // RPLSTATSBLINE  = "247";     // RESERVED by RFC 2812
        // RPLSTATSDLINE  = "250";     // RESERVED by RFC 2812
        public const string RPL_ENDOFSTATS = "219";

        public const string RPL_STATSUPTIME = "242";

        public const string RPL_STATSOLINE = "243";

        public const string RPL_UMODEIS = "221"; // RFC 1459 4.2.3.2

        public const string RPL_SERVLIST = "234";

        public const string RPL_SERVLISTEND = "235";

        public const string RPL_LUSERCLIENT = "251";

        public const string RPL_LUSEROP = "252";

        public const string RPL_LUSERUNKNOWN = "253";

        public const string RPL_LUSERCHANNELS = "254";

        public const string RPL_LUSERME = "255";

        public const string RPL_ADMINME = "256"; // RFC 1459 4.3.7

        public const string RPL_ADMINLOC1 = "257"; // RFC 1459 4.3.7

        public const string RPL_ADMINLOC2 = "258"; // RFC 1459 4.3.7

        public const string RPL_ADMINEMAIL = "259"; // RFC 1459 4.3.7

        public const string RPL_TRYAGAIN = "263";

        // RPLNONE  = "300";            'RESERVED byRFC 2812
        public const string RPL_AWAY = "301"; // RFC 1459 4.2.7

        public const string RPL_USERHOST = "302";

        public const string RPL_ISON = "303";

        public const string RPL_UNAWAY = "305"; // RFC 1459 5.1

        public const string RPL_NOWAWAY = "306"; // RFC 1459 5.1

        public const string RPL_WHOISUSER = "311"; // RFC 1459 4.5.2

        public const string RPL_WHOISSERVER = "312"; // RFC 1459 4.5.2

        public const string RPL_WHOISOPERATOR = "313"; // RFC 1459 4.5.2

        public const string RPL_WHOWASUSER = "314";

        public const string RPL_ENDOFWHO = "315";

        // RPLWHOISCHANOP  = "316";     // RESERVED by RFC 2812
        public const string RPL_WHOISIDLE = "317"; // RFC 1459 4.5.2

        public const string RPL_ENDOFWHOIS = "318"; // RFC 1459 4.5.2

        public const string RPL_WHOISCHANNELS = "319"; // RFC 1459 4.5.2

        public const string RPL_ENDOFWHOWAS = "369";

        // RPLLISTSTART  = "321";       'Obsolete (RFC 2812)
        public const string RPL_LIST = "322"; // RFC 1459 4.2.6

        public const string RPL_LISTEND = "323"; // RFC 1459 4.2.6

        public const string RPL_CHANMODEIS = "324";

        public const string RPL_UNIQOPIS = "325";

        public const string RPL_NOTOPIC = "331"; // RFC 1459 4.2.4

        public const string RPL_TOPIC = "332"; // RFC 1459 4.2.1

        public const string RPL_INVITING = "341"; // RFC 1459 4.2.7

        public const string RPL_SUMMONING = "342"; // RFC 1459 5.4

        public const string RPL_INVITELIST = "346";

        public const string RPL_ENDOFINVITELIST = "347";

        public const string RPL_EXCEPTLIST = "348";

        public const string RPL_ENDOFEXCEPTLIST = "349";

        public const string RPL_VERSION = "351"; // RFC 1459 4.3.1

        public const string RPL_WHOREPLY = "352"; // RFC 1459 4.5.1

        public const string RPL_NAMREPLY = "353"; // RFC 1459 4.2.1

        // RPLKILLDONE  = "361";        // RESERVED by RFC 2812
        // RPLCLOSING  = "362";         // RESERVED by RFC 2812
        // RPLCLOSEEND  = "363";        // RESERVED by RFC 2812
        public const string RPL_ENDOFNAMES = "366"; // RFC 1459 4.2.5

        public const string RPL_LINKS = "364"; // RFC 1459 4.3.3

        public const string RPL_ENDOFLINKS = "365"; // RFC 1459 4.3.3

        public const string RPL_BANLIST = "367";

        public const string RPL_ENDOFBANLIST = "368";

        public const string RPL_INFO = "371"; // RFC 1459 4.3.8

        public const string RPL_MOTD = "372";

        // RPLINFOSTART  = "373";       // RESERVED by RFC 2812
        public const string RPL_ENDOFINFO = "374"; // RFC 1459 4.3.8

        public const string RPL_MOTDSTART = "375";

        public const string RPL_ENDOFMOTD = "376";

        public const string RPL_YOUREOPER = "381"; // RFC 1459 4.1.5

        public const string RPL_REHASHING = "382"; // RFC 1459 5.2

        public const string RPL_YOURESERVICE = "383";

        // RPLMYPORTIS  = "384";        // RESERVED by RFC 2812
        public const string RPL_TIME = "391"; // RFC 1459 4.3.4

        // RPLUSERSSTART  = "392";     // RFC 1459 5.5
        // RPLUSERS  = "393";          // RFC 1459 5.5
        // RPLENDOFUSERS  = "394";     // RFC 1459 5.5
        // RPLNOUSERS  = "395";        // RFC 1459 5.5

        // Error messages - Compliant with RFC 2812
        public const string ERR_NOSUCHNICK = "401"; // RFC 1459 4.2.3.2

        public const string ERR_NOSUCHSERVER = "402"; // RFC 1459 4.0

        public const string ERR_NOSUCHCHANNEL = "403"; // RFC 1459 4.2.1

        public const string ERR_CANNOTSENDTOCHAN = "404"; // RFC 1459 4.4.1

        public const string ERR_TOOMANYCHANNELS = "405"; // RFC 1459 4.2.1

        public const string ERR_WASNOSUCHNICK = "406";

        public const string ERR_TOOMANYTARGETS = "407"; // RFC 1459 4.4.1

        public const string ERR_NOSUCHSERVICE = "408"; // Unimplemented

        public const string ERR_NOORIGIN = "409"; // RFC 1459 4.6.2

        public const string ERR_NORECIPIENT = "411"; // RFC 1459 4.4.1

        public const string ERR_NOTEXTTOSEND = "412"; // RFC 1459 4.4.1

        public const string ERR_NOTOPLEVEL = "413"; // RFC 1459 4.4.1

        public const string ERR_WILDTOPLEVEL = "414"; // RFC 1459 4.4.1

        public const string ERR_BADMASK = "415";

        public const string ERR_UNKNOWNCOMMAND = "421"; // RFC 1459 6.2

        public const string ERR_NOMOTD = "422";

        public const string ERR_NOADMININFO = "423"; // RFC 2811

        public const string ERR_FILEERROR = "424"; // RFC 1459 5.4

        public const string ERR_NONICKNAMEGIVEN = "431"; // RFC 1459 4.1.2

        public const string ERR_ERRONEUSNICKNAME = "432"; // RFC 1459 4.1.2

        public const string ERR_NICKNAMEINUSE = "433"; // RFC 1459 4.1.2

        public const string ERR_NICKCOLLISION = "436"; // RFC 1459 4.1.2

        public const string ERR_UNAVAILRESOURCE = "437"; // Unimplemented

        public const string ERR_USERNOTINCHANNEL = "441"; // RFC 1459 6.2

        public const string ERR_NOTONCHANNEL = "442"; // RFC 1459 4.2.2

        public const string ERR_USERONCHANNEL = "443"; // RFC 1459 4.2.7

        // ERR_NOLOGIN  = "444";         // RFC 1459 5.4
        public const string ERR_SUMMONDISABLED = "445"; // RFC 1459 5.4

        public const string ERR_USERSDISABLED = "446"; // RFC 1459 5.5

        public const string ERR_NOTREGISTERED = "451"; // RFC 1459 6.2

        /// <summary>
        /// Returned by the server by numerous commands to
        /// indicate to the client that it didn't supply enough
        /// parameters.
        /// </summary>
        public const string ERR_NEEDMOREPARAMS = "461"; // RFC 1459 4.1.1

        /// <summary>
        /// Returned by the server to any link which tries to
        /// change part of the registered details (such as
        /// password or user details from second USER message).
        /// </summary>
        public const string ERR_ALREADYREGISTERED = "462"; // RFC 1459 4.1.1

        public const string ERR_NOPERMFORHOST = "463"; // RFC 2811

        public const string ERR_PASSWDMISMATCH = "464"; // RFC 1459 4.1.5

        public const string ERR_YOUREBANNEDCREEP = "465";

        public const string ERR_YOUWILLBEBANNED = "466"; // Unimplemented

        public const string ERR_KEYSET = "467"; // RFC 1459 4.2.3.2

        public const string ERR_CHANNELISFULL = "471"; // RFC 1459 4.2.1

        public const string ERR_UNKNOWNMODE = "472"; // RFC 1459 4.2.3.2

        public const string ERR_INVITEONLYCHAN = "473"; // RFC 1459

        public const string ERR_BANNEDFROMCHAN = "474"; // RFC 1459 4.2.1

        public const string ERR_BADCHANNELKEY = "475"; // RFC 1459 4.2.1

        public const string ERR_BADCHANMASK = "476"; // RFC 1459 4.2.1, 4.2.8 - NOTE: RESERVED 6.3

        public const string ERR_NOCHANMODES = "477";

        public const string ERR_BANLISTFULL = "478";

        public const string ERR_NOPRIVILEGES = "481"; // RFC 1459 4.1.7

        public const string ERR_CHANOPRIVSNEEDED = "482"; // RFC 1459 4.2.3.2

        public const string ERR_CANTKILLSERVER = "483"; // RFC 1459 4.6.1

        public const string ERR_RESTRICTED = "484";

        public const string ERR_UNIQOPPRIVSNEEDED = "485";

        public const string ERR_NOOPERHOST = "491"; // RFC 1459 4.1.5

        // ERR_NOSERVICEHOST  = "492";   // RESERVED by RFC 2812
        public const string ERR_UMODEUNKNOWNFLAG = "501"; // RFC 1459 4.2.3.2

        public const string ERR_USERSDONTMATCH = "502"; // RFC 1459 4.2.3.2
    }
}