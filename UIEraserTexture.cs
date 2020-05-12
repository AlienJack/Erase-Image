using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(RawImage))]
public class UIEraserTexture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private RawImage image;
    public RawImage Image
    {
        get
        {
            if (image == null)
                image = GetComponent<RawImage>();

            return image;
        }
    }
    /// <summary> 画刷宽度 </summary>
    public int BrushLength = 10;
    /// <summary> 命中百分比 </summary>
    public float HitPer => hitPositions.Count * 1.0f / (BrushLength * BrushLength);

    private Canvas canvas;
    private RenderTexture tempRT;
    private RenderTexture renderTexture;
    private RenderTargetIdentifier rtID;
    private Material eraseMate;
    private HashSet<Vector2Int> hitPositions = new HashSet<Vector2Int>();

    private Texture SourceTex;

    public event Action<float> OnEraseHitPerChanged;
    public event Action OnInvalidErase;

    public bool EnableErase = true;

    private void Start()
    {
        Reset();
    }

    [ContextMenu("Reset")]
    public void Reset()
    {
        hitPositions.Clear();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (SourceTex == null) SourceTex = Image.texture;
        if (eraseMate == null) eraseMate = new Material(ShaderManager.Instance.Find("Custom/UI-Erase"));


        if (renderTexture == null)
        {
            renderTexture = RenderTexture.GetTemporary(SourceTex.width, SourceTex.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
            renderTexture.filterMode = FilterMode.Bilinear;
            renderTexture.useMipMap = false;
            renderTexture.wrapMode = TextureWrapMode.Clamp;
            rtID = new RenderTargetIdentifier(renderTexture);
        }
        if (tempRT == null)
        {
            tempRT = RenderTexture.GetTemporary(SourceTex.width, SourceTex.height);
        }

        CommandBuffer cb = new CommandBuffer();
        cb.Blit(SourceTex, rtID);
        Graphics.ExecuteCommandBuffer(cb);
        cb.Release();

        Image.texture = renderTexture;

        isDrawing = false;
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            RenderTexture.ReleaseTemporary(renderTexture);
            renderTexture = null;
        }
        if (tempRT != null)
        {
            RenderTexture.ReleaseTemporary(tempRT);
            tempRT = null;
        }
    }

    private Vector2? lastDraw;
    private void Update()
    {
        if (isDrawing)
        {
            var drawPos = GetDrawPositionByScreen();
            //对两帧中的画点的连线上做插入更多的点，保证连续
            if (lastDraw.HasValue)
            {
                var direct = (drawPos - lastDraw.Value).normalized;
                var distance = Vector2.Distance(lastDraw.Value, drawPos);
                for (float i = distance / BrushLength; i < distance; i += distance / BrushLength / 3)
                {
                    Draw(lastDraw.Value + direct * i);
                }
            }
            Draw(drawPos);
            lastDraw = drawPos;
        }
        else
        {
            if (lastDraw != null)//当放开擦除操作时,触发事件
                OnEraseHitPerChanged?.Invoke(HitPer);
            lastDraw = null;
        }
    }

    public bool isDrawing { get; private set; }
    public void OnPointerDown(PointerEventData eventData)
    {
        if (EnableErase)
            isDrawing = true;
        else
            OnInvalidErase?.Invoke();
    }

    public Vector2 GetDrawPositionByScreen()
    {
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    canvas.transform as RectTransform,
                    Input.mousePosition,
                    canvas.worldCamera,
                    out Vector3 worldPos
                    );

        var local = Image.rectTransform.InverseTransformPoint(worldPos);
        local.x = (local.x) / Image.rectTransform.rect.width;
        local.y = (local.y) / Image.rectTransform.rect.height;
        local += new Vector3(0.5f, 0.5f);

        return local;
    }

    private void Draw(Vector2 drawPoint)
    {
        var cellWidth = Image.rectTransform.rect.width / BrushLength;
        var cellHeight = Image.rectTransform.rect.height / BrushLength;
        Vector2Int hitPos = new Vector2Int((int)(drawPoint.x * Image.rectTransform.rect.width / cellWidth), (int)(drawPoint.y * Image.rectTransform.rect.height / cellHeight));
        if (hitPos.x >= 0 && hitPos.y >= 0 && !hitPositions.Contains(hitPos))
        {
            hitPositions.Add(hitPos);
        }

        eraseMate.SetVector("_Position", drawPoint);
        eraseMate.SetFloat("_Length", 1f / (BrushLength * BrushLength));
        CommandBuffer cb = new CommandBuffer();
        cb.Blit(renderTexture, tempRT, eraseMate);
        cb.Blit(tempRT, renderTexture);
        Graphics.ExecuteCommandBuffer(cb);
        cb.Release();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDrawing = false;
    }
}