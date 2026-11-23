using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const float FamiliarEvolutionDuration = 1.9f;
        private int _familiarEvolutionsPlaying, _familiarEvolutionVersion;

        private static float EvolutionAppearance(float elapsed) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.85f, 1.25f, elapsed));
        private static float EvolutionOldOpacity(float elapsed) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, .95f, elapsed));

        private IEnumerator PresentFamiliarEvolution(GameObject previous, GameObject evolved)
        {
            if (!previous || !evolved) yield break;
            int version = _familiarEvolutionVersion;
            _familiarEvolutionsPlaying++;
            var oldSprites = previous.GetComponentsInChildren<SpriteRenderer>(true);
            var newSprites = evolved.GetComponentsInChildren<SpriteRenderer>(true);
            var oldColors = Array.ConvertAll(oldSprites, sprite => sprite.color);
            var newColors = Array.ConvertAll(newSprites, sprite => sprite.color);
            var newScales = Array.ConvertAll(newSprites, sprite => sprite.transform.localScale);
            var oldRenderers = previous.GetComponentsInChildren<Renderer>(true);
            var newRenderers = evolved.GetComponentsInChildren<Renderer>(true);
            var oldEnabled = Array.ConvertAll(oldRenderers, renderer => renderer.enabled);
            var newEnabled = Array.ConvertAll(newRenderers, renderer => renderer.enabled);
            var drivers = new List<(MonoBehaviour View, bool Enabled)>();
            foreach (var view in previous.GetComponentsInChildren<MonsterTurnSpriteView>(true)) drivers.Add((view, view.enabled));
            foreach (var view in evolved.GetComponentsInChildren<MonsterTurnSpriteView>(true)) drivers.Add((view, view.enabled));
            var lights = new List<GameObject>();
            try
            {
                previous.GetComponent<ActorLocomotionView>()?.Present(Vector3.zero, 0f, false);
                evolved.GetComponent<ActorLocomotionView>()?.Present(Vector3.zero, 0f, false);
                // Prevent status tint/animation updates from overwriting the transformation fade.
                foreach (var driver in drivers) driver.View.enabled = false;
                foreach (var renderer in newRenderers) renderer.enabled = false;
                var glow = SpawnFusionGlow(evolved.transform.position, 1f, Color.clear, lights);
                var halo = SpawnFusionGlow(evolved.transform.position, 1f, Color.clear, lights);
                var orbs = new SpriteRenderer[8];
                for (int i = 0; i < orbs.Length; i++)
                    orbs[i] = SpawnFusionGlow(evolved.transform.position, .22f, Color.clear, lights);
                // Parenting guarantees cleanup if the scene unloads during the transformation.
                foreach (var light in lights)
                {
                    light.name = "Familiar evolution light";
                    light.layer = evolved.layer;
                    light.transform.SetParent(evolved.transform, true);
                }
                PlayRetailSound(RetailSfx.Restore);
                for (float elapsed = 0f; elapsed < FamiliarEvolutionDuration; elapsed += Time.unscaledDeltaTime * TowerAnimationSpeed)
                {
                    if (!previous || !evolved || version != _familiarEvolutionVersion) yield break;
                    Vector3 feet = evolved.transform.position - Vector3.up * TownActorFootOffset;
                    float oldAlpha = EvolutionOldOpacity(elapsed), appear = EvolutionAppearance(elapsed);
                    float pulse = .8f + .2f * Mathf.Sin(elapsed * 25f);
                    float tail = 1f - Mathf.InverseLerp(1.25f, FamiliarEvolutionDuration, elapsed);
                    for (int i = 0; i < oldRenderers.Length; i++)
                        if (oldRenderers[i]) oldRenderers[i].enabled = oldEnabled[i] && oldAlpha > .001f;
                    for (int i = 0; i < newRenderers.Length; i++)
                        if (newRenderers[i]) newRenderers[i].enabled = newEnabled[i] && appear > .001f;
                    TintFusionSprites(oldSprites, oldColors, 1f, oldAlpha * pulse);
                    TintFusionSprites(newSprites, newColors, 1f, appear);
                    for (int i = 0; i < newSprites.Length; i++)
                        if (newSprites[i]) newSprites[i].transform.localScale = newScales[i] * Mathf.Lerp(.85f, 1f, appear);
                    float flash = Mathf.Exp(-Mathf.Pow((elapsed - .95f) / .24f, 2f));
                    glow.transform.position = feet + Vector3.up * .85f;
                    glow.transform.localScale = Vector3.one * (1.3f + flash * 2.2f);
                    glow.color = new Color(1f, .96f, .8f, Mathf.Clamp01(elapsed * 2f) * tail * (.25f + flash * .75f));
                    halo.transform.position = feet + Vector3.up * .08f;
                    halo.transform.localScale = new Vector3(2.6f, .45f, 1f) * Mathf.Lerp(.5f, 1f, Mathf.Clamp01(elapsed));
                    halo.color = new Color(.65f, .9f, 1f, .65f * tail);
                    for (int i = 0; i < orbs.Length; i++)
                    {
                        float phase = i / (float)orbs.Length;
                        float angle = phase * Mathf.PI * 2f + elapsed * 8f;
                        float radius = elapsed < .95f ? Mathf.Lerp(.75f, .15f, elapsed / .95f) : .15f + (elapsed - .95f) * 1.4f;
                        orbs[i].transform.position = feet + new Vector3(Mathf.Cos(angle) * radius,
                            .2f + Mathf.Repeat(phase + elapsed * .75f, 1f) * 1.5f, Mathf.Sin(angle) * radius);
                        orbs[i].color = new Color(1f, .96f, .72f, tail);
                    }
                    yield return null;
                }
            }
            finally
            {
                foreach (var light in lights) if (light) Destroy(light);
                foreach (var driver in drivers) if (driver.View) driver.View.enabled = driver.Enabled;
                TintFusionSprites(newSprites, newColors, 1f, 1f);
                for (int i = 0; i < newSprites.Length; i++)
                    if (newSprites[i]) newSprites[i].transform.localScale = newScales[i];
                for (int i = 0; i < newRenderers.Length; i++)
                    if (newRenderers[i]) newRenderers[i].enabled = newEnabled[i];
                if (previous) { previous.SetActive(false); RemoveGenerated(previous); }
                if (version == _familiarEvolutionVersion)
                    _familiarEvolutionsPlaying = Mathf.Max(0, _familiarEvolutionsPlaying - 1);
            }
        }
    }
}
