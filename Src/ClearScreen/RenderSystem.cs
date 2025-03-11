// Copyright (c) Faber Leonardo. All Rights Reserved. https://github.com/FaberSanZ
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using Vortice.DXGI;
using Vortice.Direct3D12;
using Vortice.Mathematics;
using static Vortice.DXGI.DXGI;
using static Vortice.Direct3D12.D3D12;

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

    public void Run()
    {
        _window = new Window(_width, _height, "ClearScreen");
        _window.Show();

        InitializeDevice();
        InitializeSwapChain();
        InitializeRenderTargets();
        InitializeCommandList();

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

    private void RenderFrame()
    {
        _commandAllocator.Reset();
        _graphicsCommandList.Reset(_commandAllocator, null);

        int backBufferIndex = _swapChain.CurrentBackBufferIndex;
        CpuDescriptorHandle rtvHandle = _rtvHeap.GetCPUDescriptorHandleForHeapStart() + backBufferIndex * _rtvDescriptorSize;

        _graphicsCommandList.OMSetRenderTargets(rtvHandle);
        _graphicsCommandList.ClearRenderTargetView(rtvHandle, new Color4(0.0f, 0.2f, 0.4f, 1.0f));

        _graphicsCommandList.Close();
        _queue.ExecuteCommandList(_graphicsCommandList);
        _swapChain.Present(1, PresentFlags.None);
    }

    public void Dispose() 
    {
        _graphicsCommandList?.Dispose();
        _commandAllocator?.Dispose();
        _rtvHeap?.Dispose();
        _swapChain?.Dispose();
        _queue?.Dispose();
        _device?.Dispose();
    }
}
