﻿using System;
using Deremis.System;
using Deremis.Viewer;

namespace Deremis
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var app = new Application(args, new ViewerContext()))
            {
                app.Run();
            }
        }
    }
}
