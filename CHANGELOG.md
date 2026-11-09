# Changelog

All notable changes to this project will be documented in this file.

## [1.2.0] - 2026-11-08

### Security

- Cancelled articles and articles awaiting moderation could be retrieved by anyone who knew their message-id, using ARTICLE, HEAD or NEWNEWS. These lookups now return only approved, uncancelled articles.
- A user's last-login time is now recorded only after a correct password, not on every attempt.

### Fixed

#### Article content and encoding

- Articles containing non-ASCII bytes are now stored and served exactly as posted. Previously, every byte above 0x7F was replaced with `?`, which corrupted UTF-8 and other 8-bit text, 8bit MIME parts, and yEnc-encoded binaries.
- Commands are now decoded as UTF-8, as RFC 3977 requires, so command arguments such as passwords can contain non-ASCII characters. Commands containing invalid UTF-8 are rejected with `501`.
- An article containing `{` or `}` in its body or headers no longer causes an error and a dropped connection when retrieved with ARTICLE, BODY, HEAD, OVER, XOVER, XHDR or XPAT.
- Compressed responses (`XFEATURE COMPRESS GZIP`) are now complete zlib streams, including the checksum trailer.

#### Posting

- Posts that arrive across more than one network read no longer have blank lines inserted into them.
- A post is no longer cut short when one of its lines happens to end with a period.
- Cross-posted articles are now delivered to every listed newsgroup. The `Newsgroups` header is split on commas, as RFC 5536 requires. Previously a cross-post went to no group at all, but the client was still told the post succeeded.
- Several commands sent together in one packet (pipelining) are now each processed correctly.
- A line with no end-of-line marker is now limited to 1 MB, after which the connection is closed.

#### Article retrieval and overview

- ARTICLE, BODY and HEAD can now retrieve an article by message-id without first selecting a newsgroup.
- ARTICLE by message-id no longer drops the connection when the article is cross-posted to more than one group.
- `OVER <message-id>` now finds the article in any group, not just the selected one.
- OVER and XOVER now report `:bytes` as the size of the whole article (headers and body), as RFC 3977 specifies. Previously they reported a value based on the body only.
- HDR now supports the `:bytes` and `:lines` metadata items, and LIST HEADERS advertises them.
- HDR with an unknown message-id no longer drops the connection.
- XHDR with too few arguments now returns `501` instead of dropping the connection.
- XHDR and XPAT now send a status line when nothing matches, instead of a bare `.` that confused clients.
- XOVER now returns `412` instead of failing when the selected newsgroup no longer exists.

#### Response codes

- ARTICLE, BODY, HEAD and STAT now return `501` (syntax error) for a non-numeric article number, instead of `423`.
- OVER now returns `501` for a malformed range, instead of `423`.
- MODE READER now returns `201` when posting is not permitted, instead of always returning `200`.

#### Newsgroup listing

- LISTGROUP on an empty newsgroup now ends its response correctly. Previously clients could wait indefinitely.
- LISTGROUP with no argument and no selected newsgroup now sends a single `412` response instead of two.
- LISTGROUP on an empty newsgroup now clears the current article.
- LIST ACTIVE.TIMES now reports creation times in whole seconds.
- NEWGROUPS now reports `0` for missing watermarks instead of an empty field, and reports each group's posting status (`m`, `n`, `x`, `j`, `y`) the same way LIST ACTIVE does.
- NEWNEWS no longer fails when any article matches the requested date.
- CAPABILITIES no longer advertises POST when posting is not permitted.
- CAPABILITIES no longer advertises MODE-READER alongside READER, which RFC 3977 does not allow.

#### Connections

- The server no longer uses 100% CPU on a connection the client has closed, and closed connections are now released.
- A command of exactly 1,024 bytes no longer hangs the connection.
- A failed TLS handshake on the implicit-TLS port no longer stops the server accepting new connections.
- TLS handshakes now time out after 30 seconds, and a slow handshake no longer holds up other clients connecting.
- When a command fails unexpectedly, the server now sends `403` and closes the connection straight away. Previously the connection stayed open and the `403` was sent at some later, unrelated point.
- Article ranges of the form `5-` (article 5 onwards) now work.

### Modified

- Protocol data logging (`ShowData`) is now off by default.


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

