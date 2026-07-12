using UnityEngine;

/// <summary>世界坐标持续特效 — 屏障等领域用。</summary>
public static class WorldVfxSpawner
{
    public static GameObject SpawnPersistent(VfxSpawnEntry entry, Vector3 position, Vector3 forward)
    {
        if (entry == null || !entry.IsValid)
            return null;

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        Quaternion worldRot = ResolveRotation(entry, forward);
        Vector3 worldPos = position + entry.anchor.localOffset;
        var instance = Object.Instantiate(entry.prefab, worldPos, worldRot);
        if (instance != null && instance.GetComponentInChildren<Animator>() != null
            && instance.GetComponent<BarrierWorldVfx>() == null)
            instance.AddComponent<BarrierWorldVfx>();
        return instance;
    }

    public static void BeginExpire(GameObject instance)
    {
        if (instance == null) return;
        var lifecycle = instance.GetComponent<BarrierWorldVfx>();
        if (lifecycle != null)
            lifecycle.BeginExpire();
        else
            DestroyInstance(instance);
    }

    public static void DestroyInstance(GameObject instance)
    {
        if (instance != null)
            Object.Destroy(instance);
    }

    private static Quaternion ResolveRotation(VfxSpawnEntry entry, Vector3 forward)
    {
        Quaternion prefabLocal = entry.prefab.transform.localRotation;

        switch (entry.rotationMode)
        {
            case VfxRotationMode.MatchAnchor:
            case VfxRotationMode.FaceAimDirection:
                return Quaternion.LookRotation(forward, Vector3.up) * prefabLocal;
            case VfxRotationMode.FaceTarget:
            case VfxRotationMode.PrefabDefault:
            default:
                return prefabLocal;
        }
    }
}
