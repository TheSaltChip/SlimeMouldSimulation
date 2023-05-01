using System;
using UnityEngine;
using Util;

[ExecuteAlways, ImageEffectAllowedInSceneView]
public class SimulationManager : MonoBehaviour
{
        [SerializeField] private int width;
        [SerializeField] private int height;
        [SerializeField] private int moveSpeed;
        [SerializeField] private int numAgents;
        
        private GraphicsBuffer _agentsBuffer;
        private RenderTexture _trailMap;
        private RenderTexture _processedTrailMap;
        private RenderTexture _displayTexture;

        private void Start()
        {
                
        }

        private void SetShaderVariables()
        {
                
        }
}