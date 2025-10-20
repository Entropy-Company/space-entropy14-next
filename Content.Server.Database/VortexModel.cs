// File to store as much CD related database things outside of Model.cs

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

public static class VortexModel
{
    /// <summary>
    /// Stores Vortex Character data separately from the main Profile. This is done to work around a bug
    /// in EFCore migrations.
    /// <p />
    /// There is no way of forcing a dependent table to exist in EFCore (according to MS).
    /// You must always account for the possibility of this table not existing.
    /// </summary>
    public class VortexProfile
    {
        public int Id { get; set; }

        public int ProfileId { get; set; }
        public Profile Profile { get; set; } = null!;

        public float Height { get; set; } = 1f;
        public float Width { get; set; } = 1f;
    }
}
