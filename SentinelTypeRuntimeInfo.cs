using System.Collections.Generic;
using ExileCore.Shared.Interfaces;

namespace SentinelHelper;

public class SentinelTypeRuntimeInfo
{
    public CompiledPredicate Predicate { get; set; }
    public List<ISettingsHolder> ParsedSettings { get; set; }
}