﻿using System.Text.Json.Serialization;

namespace Starward.Core.Hyperion.StarRail.SimulatedUniverse;

public class SimulatedUniverseBuff
{
    [JsonPropertyName("base_type")]
    public SimulatedUniverseBuffType BuffType { get; set; }

    [JsonPropertyName("items")]
    public List<SimulatedUniverseBuffItem> Items { get; set; }
}


