using System;
using System.Collections.Generic;

/// <summary>
/// Resolves stable button IDs to prefab registrations. Additional authoring assets may
/// replace a legacy registration or supply a new ID; repeated additional IDs are errors.
/// </summary>
public sealed class BlockCatalog
{
    private readonly Dictionary<int, BlockSpec> definitions = new Dictionary<int, BlockSpec>();

    public BlockCatalog(IEnumerable<BlockSpec> legacyDefaults, IEnumerable<BlockDefinition> additionalDefinitions)
    {
        if (legacyDefaults == null)
            throw new ArgumentNullException(nameof(legacyDefaults));

        foreach (BlockSpec spec in legacyDefaults)
        {
            if (spec == null)
                throw new ArgumentException("A default block registration cannot be null.", nameof(legacyDefaults));
            if (definitions.ContainsKey(spec.BlockId))
                throw new ArgumentException("Default block IDs must be unique.", nameof(legacyDefaults));
            definitions.Add(spec.BlockId, spec);
        }

        // Missing optional Inspector data is equivalent to an empty extension list.
        // Null elements in a supplied list are different: they are broken asset bindings.
        if (additionalDefinitions == null)
            return;

        var additionalIds = new HashSet<int>();
        foreach (BlockDefinition definition in additionalDefinitions)
        {
            if (definition == null)
                throw new ArgumentException("An additional block definition cannot be null.", nameof(additionalDefinitions));
            BlockSpec spec = definition.ToSpec();
            if (!additionalIds.Add(spec.BlockId))
                throw new ArgumentException("Additional block IDs must be unique.", nameof(additionalDefinitions));

            // The asset list intentionally takes precedence over the legacy scene bindings.
            // This makes it possible to replace an old prefab without rewiring its button ID.
            definitions[spec.BlockId] = spec;
        }
    }

    public bool TryGet(int blockId, out BlockSpec specification)
    {
        return definitions.TryGetValue(blockId, out specification);
    }
}
