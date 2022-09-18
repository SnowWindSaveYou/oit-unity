using UnityEngine;
#if UNITY_POST_PROCESSING_STACK_V2
using UnityEngine.Rendering.PostProcessing;
#endif

public class OitLinkedList : IOrderIndependentTransparency
{
    private GraphicsBuffer fragmentLinkBuffer;
    private int fragmentLinkBufferId;
    private GraphicsBuffer startOffsetBuffer;
    private int startOffsetBufferId;
    private int bufferSize;
    private int bufferStride;
    private Material linkedListMaterial;
    private const int MAX_SORTED_PIXELS = 8;

    private ComputeShader oitComputeUtils;
    private int clearStartOffsetBufferKernel;
    private int dispatchGroupSizeX, dispatchGroupSizeY;

    public OitLinkedList(bool postProcess = false)
    {
        linkedListMaterial = new Material(Shader.Find("Hidden/LinkedListRendering"));
        linkedListMaterial.EnableKeyword(postProcess ? "POST_PROCESSING" : "BUILT_IN");
        int bufferWidth = Screen.width > 0 ? Screen.width : 1024;
        int bufferHeight = Screen.height > 0 ? Screen.height : 1024;

        int bufferSize = bufferWidth * bufferHeight * MAX_SORTED_PIXELS;
        int bufferStride = sizeof(uint) * 3;
        //the structured buffer contains all information about the transparent fragments
        //this is the per pixel linked list on the gpu
        fragmentLinkBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Counter, bufferSize, bufferStride);
        fragmentLinkBufferId = Shader.PropertyToID("FLBuffer");

        int bufferSizeHead = bufferWidth * bufferHeight;
        int bufferStrideHead = sizeof(uint);
        //create buffer for addresses, this is the head of the linked list
        startOffsetBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, bufferSizeHead, bufferStrideHead);
        startOffsetBufferId = Shader.PropertyToID("StartOffsetBuffer");

        oitComputeUtils = Resources.Load<ComputeShader>("OitComputeUtils");
        clearStartOffsetBufferKernel = oitComputeUtils.FindKernel("ClearStartOffsetBuffer");
        oitComputeUtils.SetBuffer(clearStartOffsetBufferKernel, startOffsetBufferId, startOffsetBuffer);
        oitComputeUtils.SetInt("bufferWidth", bufferWidth);
        dispatchGroupSizeX = Mathf.CeilToInt(bufferWidth / 32.0f);
        dispatchGroupSizeY = Mathf.CeilToInt(bufferHeight / 32.0f);
    }

    public void PreRender()
    {
        if (fragmentLinkBuffer == null || startOffsetBuffer == null)
            return;

        //reset StartOffsetBuffer to zeros
        oitComputeUtils.Dispatch(clearStartOffsetBufferKernel, dispatchGroupSizeX, dispatchGroupSizeY, 1);

        // set buffers for rendering
        Graphics.SetRandomWriteTarget(1, fragmentLinkBuffer);
        Graphics.SetRandomWriteTarget(2, startOffsetBuffer);
    }

    public void Render(RenderTexture source, RenderTexture destination)
    {
        if (fragmentLinkBuffer == null || startOffsetBuffer == null || linkedListMaterial == null)
            return;

        Graphics.ClearRandomWriteTargets();
        // blend linked list
        linkedListMaterial.SetBuffer(fragmentLinkBufferId, fragmentLinkBuffer);
        linkedListMaterial.SetBuffer(startOffsetBufferId, startOffsetBuffer);
        Graphics.Blit(source, destination, linkedListMaterial);
    }

#if UNITY_POST_PROCESSING_STACK_V2
    public void Render(PostProcessRenderContext context)
    {
        if (fragmentLinkBuffer == null || startOffsetBuffer == null || linkedListMaterial == null)
            return;

        context.command.ClearRandomWriteTargets();
        // blend linked list
        linkedListMaterial.SetBuffer(fragmentLinkBufferId, fragmentLinkBuffer);
        linkedListMaterial.SetBuffer(startOffsetBufferId, startOffsetBuffer);
        context.command.Blit(context.source, context.destination, linkedListMaterial);
    }
#endif

    public void Release()
    {
        fragmentLinkBuffer?.Dispose();
        startOffsetBuffer?.Dispose();
    }
}
