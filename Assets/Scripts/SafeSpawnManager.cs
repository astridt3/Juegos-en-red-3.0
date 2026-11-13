using UnityEngine;

public class SafeSpawnManager : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    public int SpawnCount => spawnPoints != null ? spawnPoints.Length : 0;

    public bool TryGetSpawnPosition(int index, out Vector3 position)
    {
        position = Vector3.zero;

        if (spawnPoints == null || index < 0 || index >= spawnPoints.Length || spawnPoints[index] == null)
        {
            return false;
        }

        position = spawnPoints[index].position;
        return true;
    }
}
