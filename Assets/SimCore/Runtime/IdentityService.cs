using System;

public static class IdentityService
{
    public static EntityIdentity Create(int entityId, int teamId, string role, int variantCount, int scenarioSeed, string simIdOrSalt)
    {
        var normalizedRole = role ?? string.Empty;
        var normalizedSimSalt = simIdOrSalt ?? string.Empty;
        var normalizedVariantCount = Math.Max(1, variantCount);

        var identitySeed = StableHashUtility.DeriveIdentitySeed(
            scenarioSeed,
            normalizedSimSalt,
            entityId,
            teamId,
            normalizedRole);

        var variant = (int)(identitySeed % (uint)normalizedVariantCount);
        var appearanceSeed = unchecked((int)(identitySeed >> 32));

        return new EntityIdentity(entityId, teamId, normalizedRole, variant, appearanceSeed, EntityStatus.Active);
    }
}

public static class StableHashUtility
{
    public static ulong DeriveIdentitySeed(int scenarioSeed, string simSalt, int entityId, int teamId, string role)
    {
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            hash = Fnv1a64(hash, (uint)scenarioSeed);
            hash = Fnv1a64(hash, Fnv1a32(simSalt));
            hash = Fnv1a64(hash, (uint)entityId);
            hash = Fnv1a64(hash, (uint)teamId);
            hash = Fnv1a64(hash, Fnv1a32(role));
            return Mix64(hash);
        }
    }

    public static uint Fnv1a32(string value)
    {
        unchecked
        {
            var text = value ?? string.Empty;
            uint hash = 2166136261u;
            for (var i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }

            return hash;
        }
    }

    private static ulong Fnv1a64(ulong hash, uint value)
    {
        unchecked
        {
            hash ^= value;
            hash *= 1099511628211UL;
            return hash;
        }
    }

    private static ulong Mix64(ulong value)
    {
        unchecked
        {
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9UL;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return value;
        }
    }
}
