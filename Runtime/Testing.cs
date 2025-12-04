using UnityEngine;
using System.Collections.Generic;

namespace jeanf.audiosystems.Testing
{
    public class CPUStressTest : MonoBehaviour
    {
        [Header("Stress Test Settings")]
        [SerializeField] private bool enableStressTest = false;
        [SerializeField] [Range(0, 100)] private int stressLevel = 50;
        
        [Header("Workload Types")]
        [SerializeField] private bool simulatePhysics = true;
        [SerializeField] private bool simulateAI = true;
        [SerializeField] private bool simulatePathfinding = true;
        [SerializeField] private bool simulateParticles = true;
        
        [Header("Iteration Counts")]
        [SerializeField] [Range(100, 100000)] private int physicsIterations = 10000;
        [SerializeField] [Range(100, 100000)] private int aiIterations = 5000;
        [SerializeField] [Range(10, 1000)] private int pathfindingGridSize = 100;
        
        [Header("Monitoring")]
        [SerializeField] private float currentFrameTime;
        [SerializeField] private float averageFrameTime;
        
        private Queue<float> frameTimeHistory = new Queue<float>();
        private const int HISTORY_SIZE = 60;
        
        private Vector3[] dummyPositions;
        private float[,] pathfindingGrid;

        void Start()
        {
            dummyPositions = new Vector3[1000];
            pathfindingGrid = new float[pathfindingGridSize, pathfindingGridSize];
        }

        void Update()
        {
            float startTime = Time.realtimeSinceStartup;
            
            if (enableStressTest)
            {
                int scaledIterations = Mathf.RoundToInt(stressLevel / 100f * 1f);
                
                if (simulatePhysics) SimulatePhysicsWorkload(scaledIterations);
                if (simulateAI) SimulateAIWorkload(scaledIterations);
                if (simulatePathfinding) SimulatePathfindingWorkload(scaledIterations);
                if (simulateParticles) SimulateParticleWorkload(scaledIterations);
            }
            
            currentFrameTime = (Time.realtimeSinceStartup - startTime) * 1000f;
            UpdateAverageFrameTime();
        }

        private void SimulatePhysicsWorkload(float scale)
        {
            int iterations = Mathf.RoundToInt(physicsIterations * scale);
            for (int i = 0; i < iterations; i++)
            {
                Vector3 pos = new Vector3(
                    Mathf.Sin(i * 0.01f),
                    Mathf.Cos(i * 0.01f),
                    Mathf.Tan(i * 0.001f)
                );
                Vector3 velocity = pos * Time.deltaTime;
                Vector3 acceleration = Vector3.Cross(pos, velocity);
                dummyPositions[i % dummyPositions.Length] = pos + velocity + acceleration;
            }
        }

        private void SimulateAIWorkload(float scale)
        {
            int iterations = Mathf.RoundToInt(aiIterations * scale);
            for (int i = 0; i < iterations; i++)
            {
                // Simulate decision tree evaluation
                float decision = 0f;
                for (int j = 0; j < 10; j++)
                {
                    decision += Mathf.PerlinNoise(i * 0.1f, j * 0.1f);
                    decision *= Mathf.Sign(decision - 0.5f);
                }
                
                // Simulate state machine transitions
                int state = Mathf.Abs((int)(decision * 100)) % 5;
                float utility = CalculateUtility(state, decision);
            }
        }

        private float CalculateUtility(int state, float input)
        {
            return Mathf.Pow(input, state + 1) * Mathf.Log(Mathf.Abs(input) + 1);
        }

        private void SimulatePathfindingWorkload(float scale)
        {
            int gridSize = Mathf.RoundToInt(pathfindingGridSize * Mathf.Sqrt(scale));
            gridSize = Mathf.Clamp(gridSize, 10, pathfindingGridSize);
            
            // Simulate A* heuristic calculations
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    float g = Mathf.Sqrt(x * x + y * y);
                    float h = Mathf.Abs(gridSize - x) + Mathf.Abs(gridSize - y);
                    pathfindingGrid[x % pathfindingGridSize, y % pathfindingGridSize] = g + h;
                }
            }
        }

        private void SimulateParticleWorkload(float scale)
        {
            int particles = Mathf.RoundToInt(5000 * scale);
            for (int i = 0; i < particles; i++)
            {
                Vector3 pos = Random.insideUnitSphere;
                Vector3 vel = Random.onUnitSphere * Random.value;
                Color col = new Color(Random.value, Random.value, Random.value, Random.value);
                float size = Random.value * Mathf.Sin(Time.time + i);
            }
        }

        private void UpdateAverageFrameTime()
        {
            frameTimeHistory.Enqueue(currentFrameTime);
            if (frameTimeHistory.Count > HISTORY_SIZE)
                frameTimeHistory.Dequeue();
            
            float sum = 0f;
            foreach (float t in frameTimeHistory)
                sum += t;
            averageFrameTime = sum / frameTimeHistory.Count;
        }

        void OnGUI()
        {
            if (!enableStressTest) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 150));
            GUILayout.Box($"CPU Stress Test Active\n" +
                         $"Stress Level: {stressLevel}%\n" +
                         $"Frame Time: {currentFrameTime:F2}ms\n" +
                         $"Avg Frame Time: {averageFrameTime:F2}ms\n" +
                         $"FPS: {1000f / Mathf.Max(averageFrameTime, 0.001f):F1}");
            GUILayout.EndArea();
        }
    }
}