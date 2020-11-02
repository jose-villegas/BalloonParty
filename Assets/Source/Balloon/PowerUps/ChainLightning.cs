using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ChainLightning : MonoBehaviour
{
    [SerializeField] private LineRenderer[] _lineRenderers;
    [SerializeField] private LineRenderer _glowLineRenderer;

    [Header("Segmentation")] [SerializeField]
    private float _segmentsMultiplier;

    [SerializeField] private float _randomness;

    [Header("Animation Times")] [SerializeField]
    private float _lightningJumpTime;

    private Dictionary<LineRenderer, Queue<Vector3[]>> _chainSegmentsQueue;
    private Dictionary<LineRenderer, Stack<Vector3[]>> _chainSegmentsStack;
    private List<GameEntity> _targets;

    public void Setup(List<GameEntity> targets)
    {
        _targets = targets;
        _chainSegmentsQueue = new Dictionary<LineRenderer, Queue<Vector3[]>>();
        _chainSegmentsStack = new Dictionary<LineRenderer, Stack<Vector3[]>>();

        for (int i = 0; i < targets.Count - 1; i++)
        {
            var origin = targets[i].position.Value;
            var target = targets[i + 1].position.Value;
            var segments = Mathf.FloorToInt(Vector3.Distance(origin, target) * _segmentsMultiplier);
            
            // it needs at least two entries, origin and target
            segments = Mathf.Max(segments, 2);
            
            for (int j = 0; j < _lineRenderers.Length; j++)
            {
                var lastPosition = origin;

                if (!_chainSegmentsQueue.TryGetValue(_lineRenderers[j], out var lineSegmentsQueue))
                {
                    _chainSegmentsQueue[_lineRenderers[j]] = lineSegmentsQueue = new Queue<Vector3[]>();
                }
                
                if (!_chainSegmentsStack.TryGetValue(_lineRenderers[j], out var lineSegmentsStack))
                {
                    _chainSegmentsStack[_lineRenderers[j]] = lineSegmentsStack = new Stack<Vector3[]>();
                }

                // create space for new segments list
                var randomized = new Vector3[segments];
                randomized[0] = origin;

                for (int k = 1; k < segments - 1; k++)
                {
                    // add randomness to simulate electricity
                    var token = Vector3.Lerp(origin, target, k / (float) segments);
                    lastPosition = new Vector3(token.x + Random.Range(-_randomness, _randomness),
                        token.y + Random.Range(-_randomness, _randomness), 0);
                    // register position
                    randomized[k] = lastPosition;
                }

                randomized[segments - 1] = target;
                lineSegmentsQueue.Enqueue(randomized);
                lineSegmentsStack.Push(randomized);
            }
        }
    }

    private IEnumerator ChainLightningAnimation()
    {
        var positions = new List<Vector3>[_lineRenderers.Length];
        var lineRenderers = _chainSegmentsQueue.Keys.ToArray();
        
        for (var i = 0; i < _targets.Count; i++)
        {
            var gameEntity = _targets[i];

            for (int j = 0; j < lineRenderers.Length; j++)
            {
                var lineRenderer = lineRenderers[j];
                var queue = _chainSegmentsQueue[lineRenderer];
                
                if (queue.Count > 0)
                {
                    if (positions[j] == null)
                    {
                        positions[j] = new List<Vector3>();
                    }
                    
                    positions[j].AddRange(queue.Dequeue());
                    lineRenderer.positionCount = positions[j].Count;
                    lineRenderer.SetPositions(positions[j].ToArray());
                }
            }

            if (positions != null && positions.Length > 0)
            {
                _glowLineRenderer.positionCount = positions[0].Count;
                _glowLineRenderer.SetPositions(positions[0].ToArray());
            }

            if (gameEntity.isEnabled)
            {
                gameEntity.isBalloonPowerUpHit = gameEntity.isBalloonHit = true;
            }

            yield return new WaitForSeconds(_lightningJumpTime);
        }
        
        for (var i = 0; i < _targets.Count; i++)
        {
            for (int j = 0; j < lineRenderers.Length; j++)
            {
                var lineRenderer = lineRenderers[j];
                var stack = _chainSegmentsStack[lineRenderer];
                
                if (stack.Count > 0)
                {
                    if (positions[j] == null)
                    {
                        positions[j] = new List<Vector3>();
                    }

                    var pop = stack.Pop();
                    
                    positions[j].RemoveRange(positions[j].Count - pop.Length, pop.Length);
                    lineRenderer.positionCount = positions[j].Count;
                    lineRenderer.SetPositions(positions[j].ToArray());
                }
            }

            if (positions != null && positions.Length > 0)
            {
                _glowLineRenderer.positionCount = positions[0].Count;
                _glowLineRenderer.SetPositions(positions[0].ToArray());
            }

            yield return new WaitForSeconds(_lightningJumpTime);
        }

        Destroy(gameObject);
    }

    public void Display()
    {
        StartCoroutine(ChainLightningAnimation());
    }
}