# Changelog

All notable changes to this project will be documented in this file.

## [1.1.0] - 2025-06-28

NOTE: This was a major upgrade/rewrite of a solution that was been largely unchanged since it was written in 2014.

### Modified

- Major rewrite to move from .NET Framework 4.6.1 to .NET Core 9
- Removed log4net in favor of Microsoft.Extensions.Logging dependency injection
- Refactored configuration from old App.config to modern appsettings.json

### Fixed

- Various problems with regular expressions, as now validated with unit testing

### Removed

- Unfinished IRC server support
- Unfinished IMAP server support

