﻿// Copyright (c) Faber Leonardo. All Rights Reserved. https://github.com/FaberSanZ
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using Vortice.DXGI;
using Vortice.Direct3D12;
using Vortice.Mathematics;
using static Vortice.DXGI.DXGI;
using static Vortice.Direct3D12.D3D12;
using Vortice.Dxc;
using System.Runtime.CompilerServices;
using Vortice.Direct3D;
using System.Numerics;
using System.Runtime.InteropServices;

public class RenderSystem(bool debug) : IDisposable
{
    private readonly int _width = 1200;
    private readonly int _height = 820;
    private readonly int _frameCount = 3;

    private Window _window;
    private IDXGIFactory4 _factory;
    private ID3D12Device _device;
    private ID3D12CommandQueue _queue;
    private IDXGISwapChain3 _swapChain;
    private ID3D12DescriptorHeap _rtvHeap;
    private int _rtvDescriptorSize;
    private ID3D12CommandAllocator _commandAllocator;
    private ID3D12GraphicsCommandList _graphicsCommandList;
    private ID3D12PipelineState _pipelineState;
    private ID3D12RootSignature _rootSignature;
    private ID3D12Resource _vertexBuffer;

    public void Run()
    {
        _window = new Window(_width, _height, "VertexBuffer");
        _window.Show();

        InitializeDevice();
        InitializeSwapChain();
        InitializeRenderTargets();
        InitializeCommandList();
        InitializePipeline();
        InitializeVertexBuffer();

        _window.RenderLoop(RenderFrame);
    }

    private void InitializeDevice()
    {
        _factory = CreateDXGIFactory2<IDXGIFactory4>(false);

        D3D12CreateDevice(null, out _device!);
    
        _queue = _device!.CreateCommandQueue(new CommandQueueDescription(CommandListType.Direct));
    }

    private void InitializeSwapChain()
    {
        SwapChainDescription1 swapchainDescription = new()
        {
            BufferCount = _frameCount,
            Width = _width,
            Height = _height,
            Format = Format.R8G8B8A8_UNorm,
            BufferUsage = Usage.RenderTargetOutput,
            SwapEffect = SwapEffect.FlipSequential,
            SampleDescription = new SampleDescription(1, 0),
            Flags = SwapChainFlags.AllowTearing
        };

        using (IDXGISwapChain1 swapChain1 = _factory.CreateSwapChainForHwnd(_queue, _window.Handle, swapchainDescription))
        {
            _factory.MakeWindowAssociation(_window.Handle, WindowAssociationFlags.IgnoreAltEnter);
            _swapChain = swapChain1.QueryInterface<IDXGISwapChain3>();
        }
    }

    private void InitializeRenderTargets()
    {
        _rtvHeap = _device.CreateDescriptorHeap(new DescriptorHeapDescription(DescriptorHeapType.RenderTargetView, _frameCount));
        _rtvDescriptorSize = _device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);
        CpuDescriptorHandle rtvHandle = _rtvHeap.GetCPUDescriptorHandleForHeapStart();

        for (int i = 0; i < _frameCount; i++)
        {
            using ID3D12Resource renderTarget = _swapChain.GetBuffer<ID3D12Resource>(i);
            _device.CreateRenderTargetView(renderTarget, null, rtvHandle);
            rtvHandle.Offset(_rtvDescriptorSize);
        }
    }

    private void InitializeCommandList()
    {
        _commandAllocator = _device.CreateCommandAllocator(CommandListType.Direct);
        _graphicsCommandList = _device.CreateCommandList<ID3D12GraphicsCommandList>(CommandListType.Direct, _commandAllocator, null);
        _graphicsCommandList.Close();
    }

    private void InitializePipeline()
    {
        ReadOnlyMemory<byte> vertexShaderByteCode = CompileBytecode(DxcShaderStage.Vertex, "Shaders/VertexShader.hlsl", "VS");
        ReadOnlyMemory<byte> pixelShaderByteCode = CompileBytecode(DxcShaderStage.Pixel, "Shaders/PixelShader.hlsl", "PS");

        InputElementDescription[] inputElementDescs = new[]
        {
            new InputElementDescription("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
            new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 16, 0),
        };

        RootSignatureFlags rootSignatureFlags = RootSignatureFlags.AllowInputAssemblerInputLayout;
        rootSignatureFlags |= RootSignatureFlags.DenyHullShaderRootAccess;
        rootSignatureFlags |= RootSignatureFlags.DenyDomainShaderRootAccess;
        rootSignatureFlags |= RootSignatureFlags.DenyGeometryShaderRootAccess;
        rootSignatureFlags |= RootSignatureFlags.DenyAmplificationShaderRootAccess;
        rootSignatureFlags |= RootSignatureFlags.DenyMeshShaderRootAccess;

        RootSignatureDescription1 rootSignatureDesc = new(rootSignatureFlags);
        _rootSignature = _device.CreateRootSignature(rootSignatureDesc);

        GraphicsPipelineStateDescription psoDesc = new()
        {
            RootSignature = _rootSignature,
            VertexShader = vertexShaderByteCode,
            PixelShader = pixelShaderByteCode,
            SampleMask = uint.MaxValue,
            PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
            RasterizerState = RasterizerDescription.CullCounterClockwise,
            BlendState = BlendDescription.Opaque,
            DepthStencilState = DepthStencilDescription.Default,
            RenderTargetFormats = new[] { Format.R8G8B8A8_UNorm },
            SampleDescription = SampleDescription.Default,
            InputLayout = new InputLayoutDescription(inputElementDescs),
        };

        _pipelineState = _device.CreateGraphicsPipelineState(psoDesc);
    }




    private void InitializeVertexBuffer()
    {
        List<Vertex> vertices = new()
        {
            new Vertex(new Vector3(0.0f, 0.5f, 0.0F), new Vector3(0.9f, 0.0f, 0.0F)),
            new Vertex(new Vector3(0.5f, -0.5f, 0.0f), new Vector3(0.0f, 0.9f, 0.0F)),
            new Vertex(new Vector3(-0.5f, -0.5f, 0.0f), new Vector3(0.0f, 0.0f, 0.9F)),
        };


        ulong sizeInBytes = 3 * (ulong)Unsafe.SizeOf<Vertex>();


        ResourceDescription resourceDescription = new()
        {
            Alignment = 0uL,
            Flags = ResourceFlags.None,
            Dimension = ResourceDimension.Buffer,
            Format = Format.Unknown,
            Layout = TextureLayout.RowMajor,
            Width = sizeInBytes,
            Height = 1,
            DepthOrArraySize = 1,
            MipLevels = 1,
            SampleDescription = new SampleDescription(1, 0)
        };


        _vertexBuffer = _device.CreateCommittedResource(HeapType.Upload, resourceDescription, ResourceStates.GenericRead);

        _vertexBuffer.SetData(vertices.ToArray());
    }


    private unsafe void RenderFrame()
    {
        _commandAllocator.Reset();
        _graphicsCommandList.Reset(_commandAllocator, null);

        int backBufferIndex = _swapChain.CurrentBackBufferIndex;
        CpuDescriptorHandle rtvHandle = _rtvHeap.GetCPUDescriptorHandleForHeapStart() + backBufferIndex * _rtvDescriptorSize;

        _graphicsCommandList.OMSetRenderTargets(rtvHandle);
        _graphicsCommandList.ClearRenderTargetView(rtvHandle, new Color4(0.0f, 0.2f, 0.4f, 1.0f));

        _graphicsCommandList.RSSetViewport(new Viewport(_width, _height));
        _graphicsCommandList.RSSetScissorRect(_width, _height);

        // draw


        int stride = Unsafe.SizeOf<Vertex>();
        int vertexBufferSize = 3 * stride;
        _graphicsCommandList.IASetVertexBuffers(0, new VertexBufferView(_vertexBuffer.GPUVirtualAddress, vertexBufferSize, stride));
        
        _graphicsCommandList.SetPipelineState(_pipelineState);
        _graphicsCommandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        _graphicsCommandList.DrawInstanced(3, 1, 0, 0);

        _graphicsCommandList.Close();
        _queue.ExecuteCommandList(_graphicsCommandList);
        _swapChain.Present(1, PresentFlags.None);
    }


    static ReadOnlyMemory<byte> CompileBytecode(DxcShaderStage stage, string shaderName, string entryPoint)
    {
        string assetsPath = "";
        string shaderSource = File.ReadAllText(shaderName);
        Console.WriteLine(shaderSource);
        Console.WriteLine("-----------------");

        using IDxcResult results = DxcCompiler.Compile(stage, shaderSource, entryPoint /*includeHandler: includeHandler*/);

        //if (results.GetStatus().Failure)
        //    throw new Exception(results.GetErrors());

        return results.GetObjectBytecodeMemory();
    }
    public void Dispose() 
    {
        _graphicsCommandList?.Dispose();
        _commandAllocator?.Dispose();
        _rtvHeap?.Dispose();
        _swapChain?.Dispose();
        _queue?.Dispose();
        _device?.Dispose();
        _pipelineState?.Dispose();
        _rootSignature?.Dispose();
        _vertexBuffer?.Dispose();
    }
}


[StructLayout(LayoutKind.Sequential)]
public struct Vertex
{
    public Vertex(Vector3 pos, Vector3 col)
    {
        Position = new Vector4(pos, 1);
        Color = new Vector4(col, 1);
    }

    public Vector4 Position { get; set; }
    public Vector4 Color { get; set; }
}