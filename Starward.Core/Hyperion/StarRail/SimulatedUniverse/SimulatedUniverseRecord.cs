﻿using System.Text.Json.Serialization;

namespace Starward.Core.Hyperion.StarRail.SimulatedUniverse;

public class SimulatedUniverseRecord
{
    [JsonPropertyName("basic")]
    public SimulatedUniverseRecordBasic Basic { get; set; }

    [JsonPropertyName("records")]
    public List<SimulatedUniverseRecordItem> Records { get; set; }

    [JsonPropertyName("has_data")]
    public bool HasData { get; set; }
}


