using System;

namespace SentinelHelper;

public record CompiledPredicate(string Source, Func<MonsterCount, bool> Function, string LastException);