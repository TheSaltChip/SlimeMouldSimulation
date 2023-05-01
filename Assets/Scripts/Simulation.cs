using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;
using Util;
using Random = UnityEngine.Random;

//[ExecuteAlways, ImageEffectAllowedInSceneView]
public class Simulation : MonoBehaviour
{
    public enum SpawnMode
    {
        Random,
        Point,
        InwardCircle,
        RandomCircle
    }

    private static readonly int Width = Shader.PropertyToID("Width");
    private static readonly int Agents = Shader.PropertyToID("Agents");
    private static readonly int Height = Shader.PropertyToID("Height");
    private static readonly int ColorMap = Shader.PropertyToID("ColorMap");
    private static readonly int DeltaTime = Shader.PropertyToID("DeltaTime");
    private static readonly int DecayRate = Shader.PropertyToID("DecayRate");
    private static readonly int DiffuseRate = Shader.PropertyToID("DiffuseRate");
    private static readonly int TimeVar = Shader.PropertyToID("Time");
    private static readonly int TrailMap = Shader.PropertyToID("TrailMap");
    private static readonly int NumAgents = Shader.PropertyToID("NumAgents");
    private static readonly int TargetTexture = Shader.PropertyToID("TargetTexture");
    private static readonly int DiffusedTrailMap = Shader.PropertyToID("DiffusedTrailMap");

    private int _updateKernel;
    private int _diffuseMapKernel;
    private int _drawAgentsKernel;
    private int _colorKernel;

    [SerializeField] private ComputeShader slimeComputeShader;
    [SerializeField] private ComputeShader drawAgentsComputeShader;

    [SerializeField] private SlimeSettings settings;

    [Header("Display Settings")] public bool showAgentsOnly;
    [SerializeField] private FilterMode filterMode = FilterMode.Bilinear;
    [SerializeField] private GraphicsFormat format = ShaderHelper.DefaultGraphicsFormat;

    [SerializeField] private bool saveThisFrame;
    [SerializeField] private bool stopSimulation;

    private GraphicsBuffer _agentsBuffer;
    private GraphicsBuffer _settingsBuffer;
    private RenderTexture _trailMap;
    private RenderTexture _diffusedTrailMap;
    private RenderTexture _displayTexture;

    private void Start()
    {
        _updateKernel = slimeComputeShader.FindKernel("Update");
        _colorKernel = slimeComputeShader.FindKernel("Color");
        _diffuseMapKernel = slimeComputeShader.FindKernel("Diffuse");
        _drawAgentsKernel = drawAgentsComputeShader.FindKernel("DrawAgents");

        Init();
        transform.GetComponentInChildren<MeshRenderer>().material.mainTexture = _displayTexture;
    }

    private void Init()
    {
        ShaderHelper.CreateRenderTexture(ref _trailMap, settings.width, settings.height, filterMode, format);
        ShaderHelper.CreateRenderTexture(ref _diffusedTrailMap, settings.width, settings.height, filterMode,
            format);
        ShaderHelper.CreateRenderTexture(ref _displayTexture, settings.width, settings.height, filterMode,
            format);

        var agents = new Agent[settings.numAgents];
        for (var i = 0; i < agents.Length; i++)
        {
            var centre = new Vector2(settings.width / 2f, settings.height / 2f);
            var startPos = Vector2.zero;
            var randomAngle = Random.value * Mathf.PI * 2;
            float angle = 0;

            switch (settings.spawnMode)
            {
                case SpawnMode.Point:
                    startPos = centre;
                    angle = randomAngle;
                    break;
                case SpawnMode.Random:
                    startPos = new Vector2(Random.Range(0, settings.width), Random.Range(0, settings.height));
                    angle = randomAngle;
                    break;
                case SpawnMode.InwardCircle:
                    startPos = centre + Random.insideUnitCircle * settings.height * 0.5f;
                    angle = Mathf.Atan2((centre - startPos).normalized.y, (centre - startPos).normalized.x);
                    break;
                case SpawnMode.RandomCircle:
                    startPos = centre + Random.insideUnitCircle * settings.height * 0.15f;
                    angle = randomAngle;
                    break;
            }

            var speciesMask = Vector3Int.one;
            var speciesIndex = 0;
            var numSpecies = settings.speciesSettings.Length;

            if (numSpecies != 1)
            {
                var species = Random.Range(1, numSpecies + 1);
                speciesIndex = species - 1;
                speciesMask = new Vector3Int(species == 1 ? 1 : 0, species == 2 ? 1 : 0, species == 3 ? 1 : 0);
            }

            agents[i] = new Agent()
                { Position = startPos, Angle = angle, SpeciesMask = speciesMask, SpeciesIndex = speciesIndex };
        }

        ShaderHelper.CreateStructuredBuffer(ref _agentsBuffer, agents);

        SetShaderVariables();
    }

    private void SetShaderVariables()
    {
        slimeComputeShader.SetTexture(_updateKernel, TrailMap, _trailMap);
        slimeComputeShader.SetTexture(_diffuseMapKernel, TrailMap, _trailMap);
        slimeComputeShader.SetTexture(_diffuseMapKernel, DiffusedTrailMap, _diffusedTrailMap);
        slimeComputeShader.SetTexture(_colorKernel, ColorMap, _displayTexture);
        slimeComputeShader.SetTexture(_colorKernel, TrailMap, _trailMap);

        slimeComputeShader.SetBuffer(_updateKernel, Agents, _agentsBuffer);
        slimeComputeShader.SetInt(NumAgents, settings.numAgents);

        drawAgentsComputeShader.SetBuffer(_drawAgentsKernel, Agents, _agentsBuffer);
        drawAgentsComputeShader.SetInt(NumAgents, settings.numAgents);

        slimeComputeShader.SetInt(Width, settings.width);
        slimeComputeShader.SetInt(Height, settings.height);
    }

    private void Update()
    {
        if (!saveThisFrame) return;

        saveThisFrame = false;
        SaveScreenshot();
    }

    private void FixedUpdate()
    {
        if (stopSimulation) return;

        for (var i = 0; i < settings.stepsPerFrame; i++)
        {
            RunSimulation();
        }
    }

    private void LateUpdate()
    {
        if (showAgentsOnly)
        {
            ShaderHelper.ClearRenderTexture(_displayTexture);
            drawAgentsComputeShader.SetTexture(_drawAgentsKernel, TargetTexture, _displayTexture);
            ShaderHelper.Dispatch(drawAgentsComputeShader, settings.numAgents, kernelIndex: _drawAgentsKernel);
            return;
        }

        ShaderHelper.Dispatch(slimeComputeShader, settings.width, settings.height, kernelIndex: _colorKernel);
    }

    private void RunSimulation()
    {
        var speciesSettings = settings.speciesSettings;

        ShaderHelper.CreateStructuredBuffer(ref _settingsBuffer, speciesSettings);
        slimeComputeShader.SetBuffer(_updateKernel, "Settings", _settingsBuffer);
        slimeComputeShader.SetBuffer(_colorKernel, "Settings", _settingsBuffer);

        slimeComputeShader.SetFloat(DeltaTime, Time.fixedDeltaTime);
        slimeComputeShader.SetFloat(TimeVar, Time.fixedTime);

        slimeComputeShader.SetFloat("TrailWeight", settings.trailWeight);
        slimeComputeShader.SetFloat(DecayRate, settings.decayRate);
        slimeComputeShader.SetFloat(DiffuseRate, settings.diffuseRate);
        slimeComputeShader.SetInt("NumSpecies", speciesSettings.Length);

        ShaderHelper.Dispatch(slimeComputeShader, settings.numAgents, kernelIndex: _updateKernel);
        ShaderHelper.Dispatch(slimeComputeShader, settings.width, settings.height, kernelIndex: _diffuseMapKernel);

        Graphics.Blit(_diffusedTrailMap, _trailMap);
    }

    private void SaveScreenshot()
    {
        var path = "images/";

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        saveThisFrame = false;
        path += $"/Screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";

        if (File.Exists(path))
        {
            print("File already exists");
        }

        ScreenCapture.CaptureScreenshot(path);

        print($"Saved to {path}\n");
    }

    private void OnDestroy()
    {
        ShaderHelper.Release(_agentsBuffer, _settingsBuffer);

        ShaderHelper.Release(_trailMap);
        ShaderHelper.Release(_diffusedTrailMap);
        ShaderHelper.Release(_displayTexture);
    }
}