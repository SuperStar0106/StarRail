﻿using System.Text.Json.Serialization;

namespace Starward.Core.Hyperion.StarRail.SimulatedUniverse;

public class SimulatedUniverseAvatar
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("icon")]
    public string Icon { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("rarity")]
    public int Rarity { get; set; }

    [JsonPropertyName("element")]
    public string Element { get; set; }
}


