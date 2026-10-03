using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Removes export padding in UI without changing textures or existing scene references.
public static class SpriteFraming
{
    private static readonly Dictionary<Sprite, Sprite> framed = new Dictionary<Sprite, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        SceneManager.sceneLoaded -= Apply;
        foreach (Sprite sprite in framed.Values)
            if (sprite != null) Object.Destroy(sprite);
        framed.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= Apply;
        SceneManager.sceneLoaded += Apply;
    }

    private static void Apply(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            Sprite source = image.sprite;
            if (source == null) continue;
            Rect crop;
            switch (source.name)
            {
                case "title_plaque": crop = new Rect(0.00552995f, 0.18758621f, 0.98894009f, 0.67172414f); break;
                case "slider_fill": crop = new Rect(0.00506446f, 0.24861878f, 0.99033149f, 0.50276243f); break;
                default: continue;
            }
            if (!framed.TryGetValue(source, out Sprite sprite))
            {
                Rect original = source.rect;
                Rect pixels = new Rect(original.x + crop.x * original.width,
                    original.y + crop.y * original.height,
                    crop.width * original.width, crop.height * original.height);
                sprite = Sprite.Create(source.texture, pixels, source.pivot / source.rect.size,
                    source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = source.name + "_framed";
                framed.Add(source, sprite);
            }
            image.sprite = sprite;
        }
    }
}
