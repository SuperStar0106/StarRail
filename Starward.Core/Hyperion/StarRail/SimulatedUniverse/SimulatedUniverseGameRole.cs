﻿using System.Text.Json.Serialization;

namespace Starward.Core.Hyperion.StarRail.SimulatedUniverse;

public class SimulatedUniverseGameRole
{
    [JsonPropertyName("server")]
    public string Server { get; set; }

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }
}


