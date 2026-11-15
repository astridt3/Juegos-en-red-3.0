
using UnityEngine;

public class SafeSpawnManager : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    public int SpawnCount => spawnPoints != null ? spawnPoints.Length : 0;

    public bool TryGetSpawnTransform(int index, out Transform spawn)
    {
        spawn = null;

        if (spawnPoints == null || index < 0 || index >= spawnPoints.Length || spawnPoints[index] == null)
        {
            return false;
        }

        spawn = spawnPoints[index];

        return true;
    }

    public bool TryGetSpawnPosition(int index, out Vector3 position)
    {
        position = Vector3.zero;

        if (!TryGetSpawnTransform(index, out Transform spawn))
            return false;

        position = spawn.position;

        return true;
    }
}
