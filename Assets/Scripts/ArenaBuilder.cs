using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class ArenaBuilder
{
    public const float Size = 30f;
    public const float Half = 15f;
    public const float WallHeight = 4.2f;

    static Shader litShader;
    static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

    public static Transform Build(Transform parent, out Transform[] spawnPoints, out int propCount, out int physicsCount)
    {
        materials.Clear();
        litShader = null;
        propCount = 0;

        var arena = new GameObject("Arena").transform;
        arena.SetParent(parent, false);

        Material floorMat = MakeMaterial(new Color(0.28f, 0.29f, 0.31f));
        Material wallMat = MakeMaterial(new Color(0.4f, 0.38f, 0.34f));
        Material panelMat = MakeMaterial(new Color(0.48f, 0.44f, 0.38f));
        Material crateMat = MakeMaterial(new Color(0.55f, 0.34f, 0.18f));
        Material crateAlt = MakeMaterial(new Color(0.36f, 0.22f, 0.12f));
        Material metalMat = MakeMaterial(new Color(0.22f, 0.24f, 0.27f));
        Material rustMat = MakeMaterial(new Color(0.45f, 0.2f, 0.1f));
        Material hazardMat = MakeMaterial(new Color(0.78f, 0.62f, 0.12f));
        Material lightMat = MakeEmissive(new Color(0.95f, 0.92f, 0.75f));

        Create(PrimitiveType.Cube, arena, new Vector3(0f, -0.1f, 0f), new Vector3(Size, 0.2f, Size), floorMat, true, true, "Floor");

        Create(PrimitiveType.Cube, arena, new Vector3(0f, WallHeight * 0.5f, Half), new Vector3(Size, WallHeight, 0.4f), wallMat, true, true, "WallNorth");
        Create(PrimitiveType.Cube, arena, new Vector3(0f, WallHeight * 0.5f, -Half), new Vector3(Size, WallHeight, 0.4f), wallMat, true, true, "WallSouth");
        Create(PrimitiveType.Cube, arena, new Vector3(Half, WallHeight * 0.5f, 0f), new Vector3(0.4f, WallHeight, Size), wallMat, true, true, "WallEast");
        Create(PrimitiveType.Cube, arena, new Vector3(-Half, WallHeight * 0.5f, 0f), new Vector3(0.4f, WallHeight, Size), wallMat, true, true, "WallWest");

        var ceiling = Create(PrimitiveType.Cube, arena, new Vector3(0f, WallHeight + 0.08f, 0f), new Vector3(Size, 0.16f, Size), metalMat, true, false, "Ceiling");
        ceiling.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        propCount += BuildWallPanels(arena, panelMat);
        propCount += BuildShelves(arena, metalMat, crateMat, crateAlt);
        propCount += BuildPipes(arena, rustMat, metalMat);
        propCount += BuildPalletsAndBarrels(arena, crateAlt, rustMat, hazardMat);
        propCount += BuildPillars(arena, wallMat);
        propCount += BuildLightFixtures(arena, metalMat, lightMat);
        if (XrPerformance.Enabled)
            CombineStaticMeshes(arena);
        physicsCount = BuildPhysicsCrates(parent, crateMat, hazardMat);
        spawnPoints = BuildSpawns(parent);
        ApplyIndoorLighting();
        if (!XrPerformance.Enabled)
            BuildInteriorLights(parent);
        return arena;
    }

    public static Material MakeMaterial(Color color)
    {
        if (materials.TryGetValue(color, out Material existing) && existing != null)
            return existing;

        if (litShader == null)
        {
            litShader = Shader.Find(XrPerformance.Enabled
                ? "Universal Render Pipeline/Simple Lit"
                : "Universal Render Pipeline/Lit");
            if (litShader == null)
                litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null)
                litShader = Shader.Find("Standard");
        }

        var mat = new Material(litShader);
        mat.color = color;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0.18f);
        mat.enableInstancing = true;
        materials[color] = mat;
        return mat;
    }

    static Material MakeEmissive(Color color)
    {
        Material mat = MakeMaterial(color);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 2.2f);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return mat;
    }

    static int BuildWallPanels(Transform arena, Material panelMat)
    {
        int count = 0;
        const float spacing = 0.95f;
        const float start = -13.2f;
        float[] heights = { 0.75f, 1.85f, 2.95f };

        for (int i = 0; i < 28; i++)
        {
            float along = start + i * spacing;
            for (int h = 0; h < heights.Length; h++)
            {
                float y = heights[h];
                Create(PrimitiveType.Cube, arena, new Vector3(along, y, 14.72f), new Vector3(0.82f, 0.72f, 0.08f), panelMat, false, false, "PanelN");
                Create(PrimitiveType.Cube, arena, new Vector3(along, y, -14.72f), new Vector3(0.82f, 0.72f, 0.08f), panelMat, false, false, "PanelS");
                Create(PrimitiveType.Cube, arena, new Vector3(14.72f, y, along), new Vector3(0.08f, 0.72f, 0.82f), panelMat, false, false, "PanelE");
                Create(PrimitiveType.Cube, arena, new Vector3(-14.72f, y, along), new Vector3(0.08f, 0.72f, 0.82f), panelMat, false, false, "PanelW");
                count += 4;
            }
        }

        return count;
    }

    static int BuildShelves(Transform arena, Material metalMat, Material crateMat, Material crateAlt)
    {
        int count = 0;
        float[] westZ = { -10f, -6f, -2f, 2f, 6f, 10f };
        for (int i = 0; i < westZ.Length; i++)
        {
            count += BuildShelfUnit(arena, new Vector3(-12f, 0f, westZ[i]), metalMat, crateMat, crateAlt);
            count += BuildShelfUnit(arena, new Vector3(12f, 0f, westZ[i]), metalMat, crateMat, crateAlt);
        }

        float[] northX = { -6f, -2f, 2f, 6f };
        for (int i = 0; i < northX.Length; i++)
            count += BuildShelfUnit(arena, new Vector3(northX[i], 0f, 12.2f), metalMat, crateMat, crateAlt);

        count += BuildShelfUnit(arena, new Vector3(-6f, 0f, -12.2f), metalMat, crateMat, crateAlt);
        count += BuildShelfUnit(arena, new Vector3(6f, 0f, -12.2f), metalMat, crateMat, crateAlt);
        return count;
    }

    static int BuildShelfUnit(Transform arena, Vector3 origin, Material metalMat, Material crateMat, Material crateAlt)
    {
        int count = 0;
        float[,] posts = { { -0.7f, -1.05f }, { 0.7f, -1.05f }, { -0.7f, 1.05f }, { 0.7f, 1.05f } };
        for (int i = 0; i < 4; i++)
        {
            Create(PrimitiveType.Cube, arena, origin + new Vector3(posts[i, 0], 1.15f, posts[i, 1]), new Vector3(0.08f, 2.3f, 0.08f), metalMat, true, true, "ShelfPost");
            count++;
        }

        float[] boardY = { 0.25f, 0.9f, 1.55f, 2.2f };
        for (int b = 0; b < boardY.Length; b++)
        {
            Create(PrimitiveType.Cube, arena, origin + new Vector3(0f, boardY[b], 0f), new Vector3(1.5f, 0.06f, 2.2f), metalMat, true, true, "ShelfBoard");
            count++;

            for (int c = 0; c < 3; c++)
            {
                Material mat = ((b + c) & 1) == 0 ? crateMat : crateAlt;
                float z = -0.7f + c * 0.7f;
                Create(PrimitiveType.Cube, arena, origin + new Vector3(Random.Range(-0.18f, 0.18f), boardY[b] + 0.18f, z), new Vector3(0.42f, 0.32f, 0.42f), mat, true, false, "ShelfCrate");
                count++;
            }
        }

        return count;
    }

    static int BuildPipes(Transform arena, Material rustMat, Material metalMat)
    {
        int count = 0;
        for (int i = 0; i < 8; i++)
        {
            float x = -12f + i * 3.4f;
            var pipe = Create(PrimitiveType.Cylinder, arena, new Vector3(x, WallHeight - 0.28f, 0f), new Vector3(0.12f, 14.4f, 0.12f), rustMat, false, false, "PipeX");
            pipe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            count++;
        }

        for (int i = 0; i < 6; i++)
        {
            float z = -10f + i * 4f;
            Create(PrimitiveType.Cube, arena, new Vector3(0f, WallHeight - 0.55f, z), new Vector3(26f, 0.08f, 0.16f), metalMat, false, false, "Conduit");
            count++;
        }

        return count;
    }

    static int BuildPalletsAndBarrels(Transform arena, Material woodMat, Material rustMat, Material hazardMat)
    {
        int count = 0;
        Vector3[] palletSpots =
        {
            new Vector3(-8.5f, 0.08f, 8.5f), new Vector3(8.5f, 0.08f, 8.5f),
            new Vector3(-8.5f, 0.08f, -8.5f), new Vector3(8.5f, 0.08f, -8.5f),
            new Vector3(-4f, 0.08f, 9.5f), new Vector3(4f, 0.08f, 9.5f),
            new Vector3(-9.5f, 0.08f, 0f), new Vector3(9.5f, 0.08f, 0f),
            new Vector3(-9.2f, 0.08f, 4f), new Vector3(9.2f, 0.08f, -4f),
            new Vector3(-3f, 0.08f, -9.6f), new Vector3(3f, 0.08f, -9.6f)
        };

        for (int i = 0; i < palletSpots.Length; i++)
        {
            Create(PrimitiveType.Cube, arena, palletSpots[i], new Vector3(1.1f, 0.12f, 0.9f), woodMat, true, true, "Pallet");
            count++;
            Create(PrimitiveType.Cylinder, arena, palletSpots[i] + new Vector3(-0.28f, 0.5f, 0f), new Vector3(0.42f, 0.45f, 0.42f), rustMat, true, true, "Barrel");
            count++;
            Create(PrimitiveType.Cylinder, arena, palletSpots[i] + new Vector3(0.3f, 0.42f, 0.1f), new Vector3(0.36f, 0.38f, 0.36f), i % 2 == 0 ? hazardMat : rustMat, true, true, "Barrel");
            count++;
        }

        return count;
    }

    static int BuildPillars(Transform arena, Material wallMat)
    {
        Vector3[] spots =
        {
            new Vector3(-7.5f, WallHeight * 0.5f, -7.5f),
            new Vector3(7.5f, WallHeight * 0.5f, -7.5f),
            new Vector3(-7.5f, WallHeight * 0.5f, 7.5f),
            new Vector3(7.5f, WallHeight * 0.5f, 7.5f),
            new Vector3(0f, WallHeight * 0.5f, 8.8f),
            new Vector3(0f, WallHeight * 0.5f, -10.5f),
            new Vector3(-8.8f, WallHeight * 0.5f, 0f),
            new Vector3(8.8f, WallHeight * 0.5f, 0f)
        };

        for (int i = 0; i < spots.Length; i++)
            Create(PrimitiveType.Cube, arena, spots[i], new Vector3(0.55f, WallHeight, 0.55f), wallMat, true, true, "Pillar");
        return spots.Length;
    }

    static int BuildLightFixtures(Transform arena, Material metalMat, Material lightMat)
    {
        int count = 0;
        Vector3[] spots =
        {
            new Vector3(-6f, WallHeight - 0.2f, -6f), new Vector3(6f, WallHeight - 0.2f, -6f),
            new Vector3(-6f, WallHeight - 0.2f, 6f), new Vector3(6f, WallHeight - 0.2f, 6f),
            new Vector3(0f, WallHeight - 0.2f, 0f), new Vector3(-10f, WallHeight - 0.2f, 3f),
            new Vector3(10f, WallHeight - 0.2f, -3f), new Vector3(0f, WallHeight - 0.2f, 10f)
        };

        for (int i = 0; i < spots.Length; i++)
        {
            Create(PrimitiveType.Cube, arena, spots[i], new Vector3(1.2f, 0.08f, 0.35f), metalMat, false, false, "Fixture");
            Create(PrimitiveType.Cube, arena, spots[i] + Vector3.down * 0.08f, new Vector3(1.05f, 0.05f, 0.22f), lightMat, false, false, "Lamp");
            count += 2;
        }

        return count;
    }

    static int BuildPhysicsCrates(Transform parent, Material crateMat, Material hazardMat)
    {
        var physicsRoot = new GameObject("PhysicsProps").transform;
        physicsRoot.SetParent(parent, false);

        Vector3[] spots =
        {
            new Vector3(1.6f, 0.35f, 1.2f), new Vector3(-1.8f, 0.35f, 2.1f),
            new Vector3(2.4f, 0.35f, -0.8f), new Vector3(-2.6f, 0.28f, -1.4f),
            new Vector3(0.4f, 0.28f, 3.4f), new Vector3(-0.6f, 0.45f, 4.2f),
            new Vector3(3.5f, 0.35f, 2.8f), new Vector3(-3.4f, 0.35f, 3.1f),
            new Vector3(4.2f, 0.3f, -2.2f), new Vector3(-4.1f, 0.3f, -2.6f),
            new Vector3(1.1f, 0.4f, -3.3f), new Vector3(-1.3f, 0.4f, -3.8f),
            new Vector3(5.1f, 0.32f, 1.0f), new Vector3(-5.0f, 0.32f, 0.6f),
            new Vector3(2.8f, 0.28f, 5.4f), new Vector3(-2.2f, 0.28f, 5.6f),
            new Vector3(0.2f, 0.55f, 6.2f), new Vector3(3.8f, 0.28f, -4.4f),
            new Vector3(-3.6f, 0.28f, -5.0f), new Vector3(4.8f, 0.35f, 4.6f),
            new Vector3(-4.7f, 0.35f, 4.3f), new Vector3(6.0f, 0.3f, -0.4f),
            new Vector3(-6.1f, 0.3f, 1.4f), new Vector3(0.8f, 0.32f, -5.5f)
        };

        for (int i = 0; i < spots.Length; i++)
        {
            Material mat = i % 3 == 0 ? hazardMat : crateMat;
            var crate = Create(PrimitiveType.Cube, physicsRoot, spots[i], new Vector3(0.55f, 0.5f, 0.5f), mat, true, true, "PhysicsCrate");
            var body = crate.AddComponent<Rigidbody>();
            body.mass = 6f;
            if (XrPerformance.Enabled)
            {
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.sleepThreshold = 0.08f;
                crate.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            else
            {
                body.interpolation = RigidbodyInterpolation.Interpolate;
                var obstacle = crate.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obstacle.carving = true;
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                obstacle.size = Vector3.one;
            }
        }

        return spots.Length;
    }

    static Transform[] BuildSpawns(Transform parent)
    {
        var spawns = new Transform[4];
        Vector3[] positions =
        {
            new Vector3(-12f, 0f, 12f),
            new Vector3(12f, 0f, 12f),
            new Vector3(-12f, 0f, -11f),
            new Vector3(12f, 0f, -11f)
        };

        var root = new GameObject("SpawnPoints").transform;
        root.SetParent(parent, false);
        for (int i = 0; i < positions.Length; i++)
        {
            var spawn = new GameObject("Spawn_" + i).transform;
            spawn.SetParent(root, false);
            spawn.position = positions[i];
            spawns[i] = spawn;
        }

        return spawns;
    }

    static void ApplyIndoorLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.4f, 0.41f, 0.44f);
        RenderSettings.fog = false;
        RenderSettings.reflectionIntensity = 0.3f;
    }

    static void BuildInteriorLights(Transform parent)
    {
        Vector3[] spots =
        {
            new Vector3(-6f, 3.6f, -6f), new Vector3(6f, 3.6f, -6f),
            new Vector3(-6f, 3.6f, 6f), new Vector3(6f, 3.6f, 6f)
        };

        for (int i = 0; i < spots.Length; i++)
        {
            var go = new GameObject("FillLight_" + i);
            go.transform.SetParent(parent, false);
            go.transform.position = spots[i];
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 13f;
            light.intensity = 4.5f;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.shadows = LightShadows.None;
        }
    }

    static void CombineStaticMeshes(Transform arena)
    {
        MeshRenderer[] renderers = arena.GetComponentsInChildren<MeshRenderer>();
        var groups = new Dictionary<(int matId, int sector), List<MeshRenderer>>(32);

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMaterial == null)
                continue;

            var key = (renderer.sharedMaterial.GetInstanceID(), SectorOf(renderer.bounds.center));
            if (!groups.TryGetValue(key, out List<MeshRenderer> list))
            {
                list = new List<MeshRenderer>(64);
                groups[key] = list;
            }

            list.Add(renderer);
        }

        var combinedRoot = new GameObject("CombinedMeshes").transform;
        combinedRoot.SetParent(arena, false);

        foreach (var pair in groups)
        {
            List<MeshRenderer> list = pair.Value;
            if (list.Count == 0)
                continue;

            var combines = new CombineInstance[list.Count];
            Material material = list[0].sharedMaterial;
            bool anyShadows = false;
            int valid = 0;

            for (int i = 0; i < list.Count; i++)
            {
                MeshFilter filter = list[i].GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;

                combines[valid].mesh = filter.sharedMesh;
                combines[valid].transform = combinedRoot.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                if (list[i].shadowCastingMode != ShadowCastingMode.Off)
                    anyShadows = true;
                valid++;
            }

            if (valid == 0)
                continue;

            if (valid != combines.Length)
                System.Array.Resize(ref combines, valid);

            var mesh = new Mesh { name = "Batch_" + material.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(combines, true, true);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);

            var batch = new GameObject(mesh.name);
            batch.transform.SetParent(combinedRoot, false);
            batch.AddComponent<MeshFilter>().sharedMesh = mesh;
            var batchRenderer = batch.AddComponent<MeshRenderer>();
            batchRenderer.sharedMaterial = material;
            batchRenderer.shadowCastingMode = anyShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            for (int i = 0; i < list.Count; i++)
            {
                GameObject go = list[i].gameObject;
                Collider col = go.GetComponent<Collider>();
                Object.DestroyImmediate(list[i]);
                MeshFilter filter = go.GetComponent<MeshFilter>();
                if (filter != null)
                    Object.DestroyImmediate(filter);
                if (col == null)
                    Object.DestroyImmediate(go);
            }
        }
    }

    static int SectorOf(Vector3 world)
    {
        int x = world.x >= 0f ? 1 : 0;
        int z = world.z >= 0f ? 2 : 0;
        return x + z;
    }

    public static GameObject Create(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material mat, bool collider, bool shadows, string objectName)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = objectName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        if (!collider)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);
        }

        return go;
    }
}
