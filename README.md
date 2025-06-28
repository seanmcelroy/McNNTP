McNNTP
======

A .NET implementation of the NNTP protocol to create an NNTP server in C#

Vision
------

This project aims to provide a full communications server using multiple communication protocols
to deliver information through the communication medium of the user's choice.  The first protocol
under development is a full RFC-compliant NNTP (USENET) news server that adheres to:

* [RFC 2980](http://tools.ietf.org/html/rfc2980) - Common NNTP Extensions
* [RFC 3977](http://tools.ietf.org/html/rfc3977) - Network News Transfer Protocol (NNTP)
* [RFC 4642](http://tools.ietf.org/html/rfc4642) - Using TLS with NNTP (Implicit and Explicit TLS/SSL)
* [RFC 4643](http://tools.ietf.org/html/rfc4643) - NNTP Extension for Authentication
* [RFC 4644](http://tools.ietf.org/html/rfc4644) - NNTP Extension for Streaming Feeds
* [RFC 4707](http://tools.ietf.org/html/rfc4707) - Netnews Administration System (NAS)
* [RFC 5536](http://tools.ietf.org/html/rfc5536) - Netnews Article Format
* [RFC 5537](http://tools.ietf.org/html/rfc5537) - Netnews Architecture and Protocols
* [RFC 6048](http://tools.ietf.org/html/rfc6048) - NNTP Additions to LIST Command


(See http://www.tin.org/docs.html)

Interserver distribution through NNTP and other synchronization protocols will be
implemented to allow peering among servers and inclusion of historical USENET archives along
with spidering techniques or other participatory data interchange to store historical archives
of disparate forums and other group mediums within the same messaging ecosystem.


The goal is to integrate newer protocol support into an NNTP server to improve the relevance of
the NNTP protocol and appeal of open, decentralized, pseudonynomous group communication systems.

