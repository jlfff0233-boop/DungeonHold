using UnityEngine;
using UnityEngine.UI;

// Title artwork effects use normalized image coordinates, so cropping and resizing stay aligned.
public class TitleFireBackground : MonoBehaviour
{
    private Vector3[] _braziers = {
        new Vector3(.180f,.596f,.113f), new Vector3(.940f,.646f,.096f),
        new Vector3(.667f,.516f,.068f)
    };
    // Flame crests move separately from the fixed monster faces and bodies.
    private Rect[] _flameRegions = {
        new Rect(.158f,.614f,.044f,.118f), new Rect(.924f,.663f,.034f,.103f),
        new Rect(.653f,.535f,.031f,.071f)
    };
    private Rect[] _monsterFlameRegions = {
        new Rect(.092f,.258f,.040f,.053f),
        new Rect(.787f,.735f,.039f,.065f),
        new Rect(.873f,.286f,.055f,.074f)
    };
    private const int EmbersPerBrazier = 7;
    private RawImage[] _embers;
    private Material[] _tipMaterials;
    private Texture2D _emberTexture;
    private RectTransform _art;
    private Image _original;
    private bool _originalEnabled;
    private Material _starMaterial;
    private Material _capeMaterial;
    private Texture2D _starTexture;
    private RawImage[] _dust;
    private bool _dungeon;
    private const int DustCount = 40;
    // Existing light points in the violet cavern (bottom-left artwork coordinates).
    private static readonly Vector2[] StarPositions = {
        new Vector2(0.606831f,0.480469f), new Vector2(0.595567f,0.482422f), new Vector2(0.606105f,0.49349f),
        new Vector2(0.601381f,0.479818f), new Vector2(0.631904f,0.522786f), new Vector2(0.497093f,0.587891f),
        new Vector2(0.602108f,0.503255f), new Vector2(0.558866f,0.59375f), new Vector2(0.487282f,0.589844f),
        new Vector2(0.486919f,0.573568f), new Vector2(0.658067f,0.573568f), new Vector2(0.520712f,0.51888f),
        new Vector2(0.583576f,0.475911f), new Vector2(0.480741f,0.548177f), new Vector2(0.500363f,0.526693f),
        new Vector2(0.396439f,0.594401f), new Vector2(0.5f,0.556641f), new Vector2(0.512355f,0.539714f),
        new Vector2(0.4375f,0.39974f), new Vector2(0.539608f,0.606771f), new Vector2(0.643895f,0.412109f),
        new Vector2(0.503634f,0.538411f), new Vector2(0.466206f,0.589193f), new Vector2(0.570131f,0.563151f),
        new Vector2(0.496366f,0.539714f), new Vector2(0.571584f,0.48112f), new Vector2(0.472384f,0.529297f),
        new Vector2(0.539608f,0.505208f), new Vector2(0.490189f,0.5f), new Vector2(0.478561f,0.468099f),
        new Vector2(0.498183f,0.602865f), new Vector2(0.563953f,0.557943f), new Vector2(0.586483f,0.576172f),
        new Vector2(0.518169f,0.53776f), new Vector2(0.538881f,0.53125f), new Vector2(0.56468f,0.542318f),
        new Vector2(0.544331f,0.51888f), new Vector2(0.390988f,0.645833f), new Vector2(0.575218f,0.593099f),
        new Vector2(0.527616f,0.467448f), new Vector2(0.508358f,0.553385f), new Vector2(0.439317f,0.485026f),
        new Vector2(0.555959f,0.538411f), new Vector2(0.572311f,0.520833f), new Vector2(0.393532f,0.604167f),
        new Vector2(0.655523f,0.600911f), new Vector2(0.484738f,0.510417f), new Vector2(0.521439f,0.492188f),
        new Vector2(0.563227f,0.567708f), new Vector2(0.447311f,0.424479f), new Vector2(0.543241f,0.424479f),
        new Vector2(0.59484f,0.613281f), new Vector2(0.547965f,0.404297f), new Vector2(0.398619f,0.559245f),
        new Vector2(0.46766f,0.397135f), new Vector2(0.509448f,0.636068f), new Vector2(0.65516f,0.632812f),
        new Vector2(0.655523f,0.614583f), new Vector2(0.575218f,0.77474f), new Vector2(0.59702f,0.626302f),
        new Vector2(0.387718f,0.630208f), new Vector2(0.531977f,0.53125f), new Vector2(0.385901f,0.667318f),
        new Vector2(0.379724f,0.610026f), new Vector2(0.449128f,0.435547f), new Vector2(0.509811f,0.36849f),
        new Vector2(0.385901f,0.516927f), new Vector2(0.555233f,0.580078f), new Vector2(0.573038f,0.681641f),
        new Vector2(0.56577f,0.383464f), new Vector2(0.501453f,0.486328f), new Vector2(0.421148f,0.596354f),
        new Vector2(0.649346f,0.406901f), new Vector2(0.550872f,0.474609f), new Vector2(0.52907f,0.396484f),
        new Vector2(0.617369f,0.597005f), new Vector2(0.367733f,0.63151f), new Vector2(0.503997f,0.367839f),
        new Vector2(0.523983f,0.479167f), new Vector2(0.386991f,0.593099f), new Vector2(0.55814f,0.548177f),
        new Vector2(0.37064f,0.67513f), new Vector2(0.488735f,0.389323f), new Vector2(0.484012f,0.407552f),
        new Vector2(0.467297f,0.485677f), new Vector2(0.515262f,0.380859f), new Vector2(0.372093f,0.465495f),
        new Vector2(0.380087f,0.626953f), new Vector2(0.375727f,0.60026f), new Vector2(0.608285f,0.591146f),
        new Vector2(0.553779f,0.500651f), new Vector2(0.578125f,0.478516f), new Vector2(0.381177f,0.648438f),
        new Vector2(0.377544f,0.714193f), new Vector2(0.611555f,0.614583f), new Vector2(0.37282f,0.478516f),
        new Vector2(0.596657f,0.501302f), new Vector2(0.425872f,0.464844f), new Vector2(0.603924f,0.622396f),
        new Vector2(0.587573f,0.507161f), new Vector2(0.600291f,0.608073f), new Vector2(0.579215f,0.708984f),
        new Vector2(0.374637f,0.490234f),
    };

    private void OnEnable()
    {
        _original = GetComponent<Image>();
        Sprite sprite = _original != null ? _original.sprite : null;
        Texture2D background = sprite != null ? sprite.texture : Resources.Load<Texture2D>("TitleFire/01-dungeon-gate-golem-1920x1080");
        if (background == null) return;
        _dungeon = background.name.StartsWith("01-dungeon-gate");
        bool cavern = background.name == "Gemini_Generated_Image_7un99o7un99o7un9";
        if (_dungeon)
        {
            // Bottom-left UVs. Crops stop above baskets and monster eyes.
            _flameRegions = new[] {
                new Rect(.035f,.825f,.084f,.175f),
                new Rect(.319f,.596f,.024f,.075f),
                new Rect(.466f,.389f,.027f,.058f),
                new Rect(.617f,.390f,.028f,.062f),
                new Rect(.846f,.589f,.024f,.067f),
                new Rect(.959f,.709f,.041f,.117f)
            };
            _monsterFlameRegions = new[] {
                new Rect(.557f,.285f,.031f,.045f),
                new Rect(.667f,.312f,.033f,.051f),
                new Rect(.943f,.295f,.053f,.069f),
                new Rect(.830f,.139f,.125f,.146f)
            };
            _braziers = new Vector3[_flameRegions.Length];
            for (int i = 0; i < _braziers.Length; i++)
            {
                Rect flame = _flameRegions[i];
                _braziers[i] = new Vector3(flame.center.x, flame.yMin, flame.height);
            }
        }
        if (cavern)
        {
            // Exposed flame tops only, from foreground torches to distant ramparts.
            _flameRegions = new[] {
                new Rect(.091f,.706f,.077f,.122f), new Rect(.846f,.708f,.066f,.128f),
                new Rect(.371f,.412f,.025f,.042f), new Rect(.613f,.414f,.026f,.046f),
                new Rect(.442f,.357f,.012f,.024f), new Rect(.547f,.358f,.012f,.024f),
                new Rect(.754f,.510f,.012f,.025f), new Rect(.279f,.638f,.008f,.016f),
                new Rect(.305f,.626f,.008f,.015f), new Rect(.321f,.619f,.008f,.015f),
                new Rect(.244f,.665f,.007f,.014f)
            };
            _monsterFlameRegions = new Rect[0];
            _braziers = new Vector3[_flameRegions.Length];
            for (int i = 0; i < _braziers.Length; i++)
            {
                Rect flame = _flameRegions[i];
                _braziers[i] = new Vector3(flame.center.x, flame.yMin - flame.height * .35f, flame.height);
            }
        }
        Transform previous = transform.Find("AnimatedTitleArtwork");
        if (previous != null)
        {
            previous.gameObject.SetActive(false);
            Destroy(previous.gameObject);
        }
        _original = GetComponent<Image>();
        if (_original != null)
        {
            _originalEnabled = _original.enabled;
            _original.enabled = false;
        }
        RawImage backdrop = CreateImage("AnimatedTitleArtwork", transform, background);
        _art = backdrop.rectTransform;
        _art.anchorMin = _art.anchorMax = new Vector2(.5f,.5f);
        AspectRatioFitter fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = (float)background.width / background.height;
        if (_dungeon)
        {
            Shader capeShader = Resources.Load<Shader>("TitleFire/CapeWind");
            if (capeShader != null)
            {
                _capeMaterial = new Material(capeShader);
                // Keep the background on UI/Default. Only this transparent cloth overlay moves.
                RawImage cape = CreateImage("CapeClothMotion", _art, background);
                cape.rectTransform.anchorMin = Vector2.zero;
                cape.rectTransform.anchorMax = Vector2.one;
                cape.rectTransform.offsetMin = cape.rectTransform.offsetMax = Vector2.zero;
                cape.material = _capeMaterial;
            }
        }
        _tipMaterials = new Material[_flameRegions.Length+_monsterFlameRegions.Length];
        Shader tipShader = Resources.Load<Shader>("TitleFire/FlameTipMotion");
        if (cavern && tipShader != null)
        {
            _starMaterial = new Material(tipShader);
            _starMaterial.SetFloat("_Twinkle", 1);
            _starMaterial.SetVector("_Region", new Vector4(0, 0, 1, 1));
            backdrop.material = _starMaterial;
        }
        if (tipShader != null)
            for (int i = 0; i < _tipMaterials.Length; i++)
            {
                bool monster = i >= _flameRegions.Length;
                Rect region = monster ? _monsterFlameRegions[i-_flameRegions.Length] : _flameRegions[i];
                RawImage tip = CreateImage("FlameTip"+i,_art,background);
                tip.uvRect = region;
                tip.rectTransform.anchorMin = region.min;
                tip.rectTransform.anchorMax = region.max;
                tip.rectTransform.offsetMin = tip.rectTransform.offsetMax = Vector2.zero;
                var material = new Material(tipShader);
                material.SetVector("_Region",new Vector4(region.x,region.y,region.width,region.height));
                material.SetFloat("_Sway",monster ? 1.5f : i == 0 ? 2f : 1.6f);
                _tipMaterials[i] = material;
                tip.material = material;
            }
        _emberTexture = new Texture2D(32,32,TextureFormat.RGBA32,false);
        _emberTexture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float radius = Vector2.Distance(new Vector2(x,y),Vector2.one*15.5f)/15.5f;
                _emberTexture.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-radius),1.5f)));
            }
        _emberTexture.Apply();
        if (_dungeon)
        {
            _dust = new RawImage[DustCount];
            for (int i = 0; i < _dust.Length; i++)
            {
                _dust[i] = CreateImage("LightDust" + i, _art, _emberTexture);
                _dust[i].color = Color.clear;
            }
        }
        if (cavern && _starMaterial != null)
        {
            // Each star gets a mask and a shared phase across its entire painted core.
            int maskWidth = 1024;
            int maskHeight = Mathf.RoundToInt(maskWidth * (float)background.height / background.width);
            _starTexture = new Texture2D(maskWidth, maskHeight, TextureFormat.RGBA32, false, true);
            _starTexture.wrapMode = TextureWrapMode.Clamp;
            _starTexture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[maskWidth * maskHeight];
            float radius = maskHeight * .006f;
            for (int i = 0; i < StarPositions.Length; i++)
            {
                Vector2 center = new Vector2(StarPositions[i].x * maskWidth, StarPositions[i].y * maskHeight);
                int extent = Mathf.CeilToInt(radius);
                for (int y = Mathf.Max(0, (int)center.y - extent); y <= Mathf.Min(maskHeight - 1, (int)center.y + extent); y++)
                    for (int x = Mathf.Max(0, (int)center.x - extent); x <= Mathf.Min(maskWidth - 1, (int)center.x + extent); x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), center) / radius;
                        float coverage = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.5f, 1, distance));
                        int index = y * maskWidth + x;
                        if (coverage > pixels[index].a)
                            pixels[index] = new Color(Mathf.Repeat(i * .618034f, 1), (i % 7) / 6f, 0, coverage);
                    }
            }
            _starTexture.SetPixels(pixels);
            _starTexture.Apply(false, true);
            _starMaterial.SetTexture("_StarMask", _starTexture);
        }
        _embers = new RawImage[_braziers.Length*EmbersPerBrazier];
        for (int i = 0; i < _embers.Length; i++)
        {
            _embers[i] = CreateImage("Ember"+i,_art,_emberTexture);
            _embers[i].color = Color.clear;
        }
    }

    private static RawImage CreateImage(string label,Transform parent,Texture texture)
    {
        var obj = new GameObject(label,typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent,false);
        RawImage image = obj.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    private void LateUpdate()
    {
        if (_art == null) return;
        float time = Time.unscaledTime * 1.5f;
        if (_starMaterial != null) _starMaterial.SetFloat("_Clock", Time.unscaledTime);
        if (_capeMaterial != null) _capeMaterial.SetFloat("_Clock", Time.unscaledTime);
        for (int i = 0; i < _tipMaterials.Length; i++)
            if (_tipMaterials[i] != null)
                _tipMaterials[i].SetFloat("_Clock",time*(1.1f+i*.037f)+i*2.73f);
        if (_dust != null)
        {
            float clock = Time.unscaledTime;
            for (int i = 0; i < _dust.Length; i++)
            {
                // Deterministic phases avoid disturbing gameplay's random state.
                float seed = Mathf.Repeat(i * .618034f, 1);
                // Follow the actual light shaft, not the entire blue dungeon interior.
                // It enters at the upper center-right and spreads gently down to the left.
                float row = ((i / 8) + .5f + (seed - .5f) * .85f) / 5f;
                float y = Mathf.Lerp(.60f, .995f, row)
                    + Mathf.Sin(clock * (.26f + seed * .11f) + i * 1.7f) * .012f;
                float beamHeight = Mathf.InverseLerp(.60f, 1f, y);
                float spread = Mathf.Lerp(.085f, .045f, beamHeight);
                float center = Mathf.Lerp(.545f, .605f, beamHeight);
                float horizontalSeed = Mathf.Repeat(i * .7548777f, 1);
                float across = ((i % 8) + .5f + (horizontalSeed - .5f) * .9f) / 4f - 1;
                float x = center + across * spread
                    + Mathf.Sin(clock * (.23f + seed * .12f) + i * 2.4f) * .006f;
                RectTransform mote = _dust[i].rectTransform;
                mote.anchorMin = mote.anchorMax = new Vector2(x, y);
                mote.anchoredPosition = Vector2.zero;
                float diameter = _art.rect.height * Mathf.Lerp(.0018f, .0032f, seed);
                mote.sizeDelta = Vector2.one * diameter;
                float edge = Mathf.Abs(x - center) / spread;
                float beam = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, 1f, edge)))
                    * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.60f, .72f, y));
                float shimmer = .92f + .08f * Mathf.Sin(clock * .32f + i);
                float opacity = Mathf.Lerp(.42f, .62f, seed);
                _dust[i].color = new Color(.72f, .80f, .88f, beam * shimmer * opacity);
            }
        }
        for (int i = 0; i < _braziers.Length; i++)
        {
            Vector3 point = _braziers[i];
            float size = _art.rect.height*point.z;
            for (int j = 0; j < EmbersPerBrazier; j++)
            {
                RawImage ember = _embers[i*EmbersPerBrazier+j];
                float cycle = Mathf.Repeat(time/(1.8f+j*.21f+i*.05f)+i*.173f+j*.151f,1);
                float life = Mathf.Clamp01(cycle/.9f);
                RectTransform spark = ember.rectTransform;
                spark.anchorMin = spark.anchorMax = new Vector2(point.x,point.y);
                spark.anchoredPosition = new Vector2(
                    (Mathf.Sin(time*1.2f+i+j*2.3f)*.22f+(j-3)*.04f*life)*size,
                    size*(.55f+life*1.55f));
                float diameter = Mathf.Max(i==0 ? 2f : .75f,size*(.035f+j%3*.005f))*(1-life*.45f);
                spark.sizeDelta = new Vector2(diameter*.7f,diameter*1.3f);
                float alpha = Mathf.Sin(life*Mathf.PI)*(.85f+.15f*Mathf.Sin(time*7+j));
                ember.color = new Color(1,Mathf.Lerp(.75f,.30f,life),.10f,alpha*(i==0 ? 1f : .8f));
            }
        }
    }

    private void OnDisable()
    {
        _dust = null;
        if (_tipMaterials != null)
            foreach (Material material in _tipMaterials) if (material != null) Destroy(material);
        if (_starMaterial != null) Destroy(_starMaterial);
        if (_capeMaterial != null) Destroy(_capeMaterial);
        if (_starTexture != null) Destroy(_starTexture);
        if (_emberTexture != null) Destroy(_emberTexture);
        if (_art != null)
        {
            _art.gameObject.SetActive(false);
            Destroy(_art.gameObject);
            _art = null;
        }
        if (_original != null) _original.enabled = _originalEnabled;
    }
}
