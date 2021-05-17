using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using DefaultEcs.System;
using Deremis.Engine.Systems.Components;
using Deremis.Platform;
using Veldrid;

namespace Deremis.Engine.Systems
{
    public abstract class DrawableSystem : AEntityMultiMapSystem<float, Drawable>
    {
        public const uint MAX_INSTANCE_COUNT = 1024;
        public const uint INSTANCE_BUFFER_SIZE = 64 * MAX_INSTANCE_COUNT;
        protected readonly Application app;
        protected readonly World world;
        protected readonly CommandList mainCommandList;

        private readonly ConcurrentDictionary<string, DrawState> drawStates = new ConcurrentDictionary<string, DrawState>();

        protected DrawableSystem(Application app, World world, EntityMultiMap<Drawable> map) : base(map)
        {
            this.app = app;
            this.world = world;
            this.mainCommandList = app.Factory.CreateCommandList();
        }

        protected static bool CanRenderToScreen(in Render render)
        {
            return render.Screen;
        }

        protected static bool CanRenderToShadowMap(in Render render)
        {
            return render.Shadows;
        }

        protected DrawState GetState(string key, bool isInstanced)
        {
            if (!drawStates.ContainsKey(key))
            {
                var commandList = app.Factory.CreateCommandList();
                var state = new DrawState { CommandList = commandList, Key = key };
                if (isInstanced)
                {
                    state.Worlds = new List<Matrix4x4>((int)MAX_INSTANCE_COUNT);
                    state.InstanceBuffer = app.Factory.CreateBuffer(new BufferDescription(INSTANCE_BUFFER_SIZE, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
                }
                if (drawStates.TryAdd(key, state))
                {
                    return state;
                }
                else
                {
                    commandList.Dispose();
                    return null;
                }
            }
            return drawStates[key];
        }
    }
}