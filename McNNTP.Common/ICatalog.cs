namespace McNNTP.Common;

using System;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// An ICatalog is a container that holds other catalogs hierarchically and/or messages.
/// </summary>
public interface ICatalog
{
    /// <summary>
    /// Gets or sets the unique identifier for the catalog within a store.
    /// </summary>
    /// <remarks>This field must be unique.</remarks>
    [NotNull]
    string Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the catalog within a store and catalog container.
    /// </summary>
    /// <remarks>This field need not be unique.</remarks>
    [NotNull]
    string Name { get; set; }

    /// <summary>
    /// Gets or sets the date and time the catalog was created, if known.
    /// </summary>
    DateTime? CreateDateUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of total messages in the catalog.
    /// </summary>
    int MessageCount { get; set; }

    /// <summary>
    /// Gets the highest message number in the catalog.
    /// </summary>
    int? HighWatermark { get; }

    /// <summary>
    /// Gets a value indicating whether the catalog is 'moderated' and requires special permission or approval to post.
    /// </summary>
    bool Moderated { get; }

    /// <summary>
    /// Gets the 'owner' of the newsgroup.  Such groups are personal groups,
    /// typically personal mailboxes, and can only be enumerated by the user that owns
    /// them.
    /// </summary>
    IIdentity? Owner { get; }
}
