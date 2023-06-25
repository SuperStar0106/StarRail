﻿using System.Text.Json.Serialization;

namespace Starward.Core.Hyperion.StarRail.SimulatedUniverse;

public class SimulatedUniverseBuffItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("is_evoluted")]
    public bool IsEvoluted { get; set; }

    [JsonPropertyName("rank")]
    public int Rank { get; set; }
}


