﻿using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.UObject;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Wwise.Exports;

public class AkAudioEvent : UObject {
    public WwiseLocalizedEventCookedData EventCookedData { get; set; } = new();

    public override void Deserialize(FAssetArchive Ar, long validPos) {
        base.Deserialize(Ar, validPos);

        if (TryGetValue<WwiseLocalizedEventCookedData>(out var eventCookedData, nameof(EventCookedData))) {
            EventCookedData = eventCookedData;
            return;
        }

        if (validPos - Ar.Position > 12) {
            // this is an ugly workaround, the struct itself sometimes is serialized without properties, and EventCookedData + RequiredBank is written manually.
            EventCookedData.Name = "EventCookedData";
            EventCookedData.Class = new UScriptClass("WwiseLocalizedEventCookedData");
            EventCookedData.Flags = Class!.Flags | EObjectFlags.RF_ClassDefaultObject;
            EventCookedData.Outer = this;
            EventCookedData.Deserialize(Ar, validPos);

            Ar.Position = validPos; // skip last 12 bytes (RequiredBank)
        }
    }

    protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer) {
        base.WriteJson(writer, serializer);
        if (Properties.Count == 0)
        {
            writer.WritePropertyName("EventCookedData");
            serializer.Serialize(writer, EventCookedData);
        }
    }
}
