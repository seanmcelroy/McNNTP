McNNTP
======

A .NET implementation of the NNTP protocol to create an NNTP server in C#

Vision
------

This project aims to provide a full communications server using multiple communication protocols
to deliver information through the communication medium of the user's choice.  The first protocol
under development is a full RFC-compliant NNTP (USENET) news server that adheres to:

* RFC 2980 - Common NNTP Extensions
* RFC 3977 - Network News Transfer Protocol (NNTP)
* RFC 4642 - Using TLS with NNTP (Implicit and Explicit TLS/SSL)
* RFC 4643 - NNTP Extension for Authentication
* RFC 5536 - Netnews Article Format
* RFC 6048 - NNTP Additions to LIST Command

Other proprietary extensions such as GZIP compression, may be added at a later date.

Inclusion of other non-NNTP communication protocols, such as RSS and Atom, as well as a web-based
forum user interface built on a REST API are planned to provide multiple ways to interact with
the same data through users' preferred communication mediums and clients.  

Finally, interserver distribution through NNTP and other synchronization protocols will be
implemented to allow peering among servers and inclusion of historical USENET archives along
with spidering techniques or other participatory data interchange to store historical archives
of disparate forums and other group mediums within the same messaging ecosystem.


The goal is to integrate newer protocol support into an NNTP server to improve the relevance of
the NNTP protocol and appeal of open, decentralized, pseudonynomous group communication systems.

