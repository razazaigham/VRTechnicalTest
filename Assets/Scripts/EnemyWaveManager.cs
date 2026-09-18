using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyWaveManager : MonoBehaviour
{
    public static EnemyWaveManager Instance { get; private set; }

    public Transform[] spawnPoints;
    public int maxAlive = 10;
    public float spawnInterval = 0.7f;
    public float wavePause = 2.6f;

    static readonly int[] WaveSizes = { 3, 5, 7, 10 };

    public readonly List<Enemy> LiveEnemies = new List<Enemy>(12);
    GameObject template;
    int waveIndex;
    int remainingToSpawn;

    public string StatusLine => $"Wave {waveIndex + 1}  |  Alive {LiveEnemies.Count}/{Mathf.Min(WaveSize, maxAlive)}";
    int WaveSize => WaveSizes[Mathf.Clamp(waveIndex, 0, WaveSizes.Length - 1)];

    void Awake()
    {
        Instance = this;
    }

    public void Begin()
    {
        if (template == null)
        {
            template = Enemy.CreateTemplate();
            template.transform.SetParent(transform, false);
        }
        StartCoroutine(RunWaves());
    }

    public void NotifyEnemyDied(Enemy enemy)
    {
        LiveEnemies.Remove(enemy);
    }

    IEnumerator RunWaves()
    {
        yield return null;
        while (true)
        {
            remainingToSpawn = WaveSize;
            yield return StartCoroutine(SpawnWave());

            while (LiveEnemies.Count > 0)
                yield return null;

            yield return new WaitForSeconds(wavePause);
            if (waveIndex < WaveSizes.Length - 1)
                waveIndex++;
        }
    }

    IEnumerator SpawnWave()
    {
        int spawnCursor = 0;
        while (remainingToSpawn > 0)
        {
            while (LiveEnemies.Count >= maxAlive)
                yield return null;

            Transform spawn = NextSpawn(ref spawnCursor);
            Vector3 pos = spawn.position + new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.6f, 0.6f));
            var go = Instantiate(template, pos, Quaternion.LookRotation(Vector3.back));
            go.name = "Enemy";
            var enemy = go.GetComponent<Enemy>();
            enemy.Activate(pos);
            LiveEnemies.Add(enemy);
            remainingToSpawn--;
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    Transform NextSpawn(ref int cursor)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return transform;

        Transform spawn = spawnPoints[cursor % spawnPoints.Length];
        cursor++;
        return spawn;
    }
}
