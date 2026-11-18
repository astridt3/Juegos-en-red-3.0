
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DownEnemySpawner : NetworkBehaviour
{
    [Header("Enemy")]
    [SerializeField] private NetworkPrefabRef enemyPrefab;
    [SerializeField] private NetworkPrefabRef enemyPrefab2;
    [SerializeField] private NetworkPrefabRef enemyPrefab3;
    [SerializeField] private NetworkPrefabRef enemyPrefab4;
    [SerializeField] private NetworkPrefabRef enemyPrefab5;
    [SerializeField] private NetworkPrefabRef enemyPrefab6;
    [SerializeField] private NetworkPrefabRef enemyPrefab7;

    [Header("Spawn")]
    [SerializeField] private float spawnInterval = 3f;
    [SerializeField] private float margin = 1f;

    [Header("Spawn por nivel")]
    [SerializeField] private float level1Spawn = 4f;
    [SerializeField] private float level2Spawn = 2.5f;
    [SerializeField] private float level3Spawn = 1f;

    private TickTimer spawnTimer;
    private Camera mainCamera;

    public override void Spawned()
    {
        mainCamera = Camera.main;

        if (Object.HasStateAuthority)
        {
            int level = SceneManager.GetActiveScene().buildIndex;

            if (level == 1)
                spawnInterval = level1Spawn;
            else if (level == 2)
                spawnInterval = level2Spawn;
            else if (level == 3)
                spawnInterval = level3Spawn;

            spawnTimer = TickTimer.CreateFromSeconds(Runner, spawnInterval);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (!spawnTimer.Expired(Runner))
            return;

        SpawnEnemy();

        spawnTimer = TickTimer.CreateFromSeconds(Runner, spawnInterval);
    }

    private void SpawnEnemy()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;

            if (mainCamera == null)
                return;
        }

        Vector3 leftEdge = mainCamera.ViewportToWorldPoint(new Vector3(0f, 1f, 0f));
        Vector3 rightEdge = mainCamera.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));

        float randomX = Random.Range(leftEdge.x, rightEdge.x);

        Vector3 spawnPosition = new Vector3(randomX, leftEdge.y + margin, 0f);

        NetworkPrefabRef selectedEnemy;

        int randomEnemy = Random.Range(0, 7);

        if (randomEnemy == 0)
            selectedEnemy = enemyPrefab;
        else if (randomEnemy == 1)
            selectedEnemy = enemyPrefab2;
        else if (randomEnemy == 2)
            selectedEnemy = enemyPrefab3;
        else if (randomEnemy == 3)
            selectedEnemy = enemyPrefab4;
        else if (randomEnemy == 4)
            selectedEnemy = enemyPrefab5;
        else if (randomEnemy == 5)
            selectedEnemy = enemyPrefab6;
        else
            selectedEnemy = enemyPrefab7;

        Runner.Spawn(selectedEnemy, spawnPosition, Quaternion.identity);
    }
}
