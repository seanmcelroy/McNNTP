// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ICatalog.cs" company="Copyright Sean McElroy">
//   Copyright Sean McElroy
// </copyright>
// <summary>
//   An ICatalog is a container that holds other catalogs hierarchically and/or messages
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Common
{
    using System;

    using JetBrains.Annotations;

    /// <summary>
    /// An ICatalog is a container that holds other catalogs hierarchically and/or messages
    /// </summary>
    public interface ICatalog
    {
        /// <summary>
        /// Gets or sets the unique identifier for the catalog within a store
        /// </summary>
        /// <remarks>This field must be unique</remarks>
        [NotNull]
        string Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the catalog within a store and catalog container
        /// </summary>
        /// <remarks>This field need not be unique</remarks>
        [NotNull]
        string Name { get; set; }

        /// <summary>
        /// Gets or sets the date and time the catalog was created, if known
        /// </summary>
        DateTime? CreateDateUtc { get; set; }

        /// <summary>
        /// Gets or sets the number of total messages in the catalog
        /// </summary>
        int MessageCount { get; set; }

        /// <summary>
        /// Gets the highest message number in the catalog
        /// </summary>
        int? HighWatermark { get; }
    }
}