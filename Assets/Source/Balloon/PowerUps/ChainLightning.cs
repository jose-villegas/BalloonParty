using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ChainLightning : MonoBehaviour
{
    [SerializeField] private LineRenderer[] _lineRenderers;
    [SerializeField] private LineRenderer _glowLineRenderer;
    
    [Header("Segmentation")]
    [SerializeField] private float _segmentsMultiplier;
    [SerializeField] private float _randomness;
    
    [Header("Animation Times")]
    [SerializeField] private float _appearAnimationTime;
    [SerializeField] private float _holdAnimationTime;
    [SerializeField] private float _dissapearAnimation;

    public void Setup(List<GameEntity> targets)
    {
        var positions = targets.Select(x => x.position.Value).ToArray();
        var chainPositions = new List<Vector3>[_lineRenderers.Length];

        // initialize chain positions arrays
        for (int i = 0; i < chainPositions.Length; i++)
        {
            chainPositions[i] = new List<Vector3>();
        }

        for (int i = 0; i < positions.Length - 1; i++)
        {
            var origin = positions[i];
            var target = positions[i + 1];
            var segments = Mathf.FloorToInt(Vector3.Distance(origin, target) * _segmentsMultiplier);

            for (int j = 0; j < _lineRenderers.Length; j++)
            {
                var lastPosition = origin;
                chainPositions[j].Add(origin);

                for (int k = 1; k < segments - 1; k++)
                {
                    // add randomness to simulate electricity
                    var token = Vector3.Lerp(origin, target, k / (float) segments);
                    lastPosition = new Vector3(token.x + Random.Range(-_randomness, _randomness),
                        token.y + Random.Range(-_randomness, _randomness), token.z);
                    // register position
                    chainPositions[j].Add(lastPosition);
                }
                
                chainPositions[j].Add(target);
            }
        }

        for (int i = 0; i < _lineRenderers.Length; i++)
        {
            _lineRenderers[i].positionCount = chainPositions[i].Count;
            _lineRenderers[i].SetPositions(chainPositions[i].ToArray());
        }

        StartCoroutine(ChainLightningAnimation(chainPositions, targets));
    }

    private IEnumerator ChainLightningAnimation(List<Vector3>[] chainPositions, List<GameEntity> gameEntities)
    {
        var chainCount = chainPositions[0].Count;

        for (int i = 1; i < chainCount - 1; i++)
        {
            for (int j = 0; j < _lineRenderers.Length; j++)
            {
                _glowLineRenderer.positionCount = i + 1;
                _lineRenderers[j].positionCount = i + 1;
                _lineRenderers[j].SetPositions(chainPositions[j].ToArray());
                _glowLineRenderer.SetPositions(chainPositions[j].ToArray());
            }
            
            yield return new WaitForSeconds(_appearAnimationTime / chainCount);
        }

        foreach (var t in gameEntities)
        {
            if (!t.isEnabled) continue;
            
            t.isBalloonPowerUpHit = t.isBalloonHit = true;
            yield return new WaitForSeconds(_holdAnimationTime / gameEntities.Count);
        }
        
        for (int i = chainCount -2; i >= 0; i--)
        {
            for (int j = 0; j < _lineRenderers.Length; j++)
            {
                if (i == chainCount -2)
                {
                    chainPositions[j].Reverse();
                }
                
                _glowLineRenderer.positionCount = i + 1;
                _lineRenderers[j].positionCount = i + 1;
                _lineRenderers[j].SetPositions(chainPositions[j].ToArray());
                _glowLineRenderer.SetPositions(chainPositions[j].ToArray());
            }
            
            yield return new WaitForSeconds(_dissapearAnimation / chainCount);
        }
    }
}