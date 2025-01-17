﻿/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See src/Resources/Files/License.txt for full licensing and attribution      //
// details.                                                                    //
// .                                                                           //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using PaintDotNet;
using State = pyrochild.effects.common.DiskBackedSurfaceState;
using System.Drawing;
using System.Threading;

namespace pyrochild.effects.common
{
    public sealed class DiskBackedSurface
        : IDisposable,
          ICloneable
    {
        private string backingfile;
        private string backingfolder;
        private State state;
        private Surface surface;
        private int width;
        private int height;

        private void Initialize(string backingfolder)
        {
            width = surface.Width;
            height = surface.Height;
            this.backingfolder = backingfolder;
            backingfile = Path.Combine(backingfolder, Path.GetRandomFileName());
            state = State.Memory;
        }

        public DiskBackedSurface(int width, int height, string backingfolder)
        {
            surface = new Surface(width, height);
            Initialize(backingfolder);
        }

        public DiskBackedSurface(Size size, string backingFolder)
        {
            surface = new Surface(size);
            Initialize(backingFolder);
        }

        public DiskBackedSurface(Surface surface, bool takeownership, string backingFolder)
        {
            if (takeownership)
            {
                this.surface = surface;
            }
            else
            {
                this.surface = surface.Clone();
            }
            Initialize(backingFolder);
        }

        private DiskBackedSurface(DiskBackedSurface cloneMe)
        {
            surface = cloneMe.surface;
            width = cloneMe.width;
            height = cloneMe.height;
            backingfolder = cloneMe.backingfolder;
            backingfile= cloneMe.backingfile;
            state = cloneMe.state;
        }

        public string BackingFilePath { get { return backingfile; } }
        public Surface Surface { get { return surface; } }
        public int Width { get { return width; } }
        public int Height { get { return height; } }
        public Size Size { get { return new Size(width, height); } }
        public State State { get { return state; } }
        public Rectangle Bounds { get { return new Rectangle(0, 0, width, height); } }

        public void ToMemory()
        {
            if (state == State.Memory) { return; }

            FileStream fs = new FileStream(backingfile, FileMode.Open, FileAccess.Read);
            try
            {
                surface = SurfaceSerializer.Deserialize(fs);
                state = State.Memory;
            }
            catch (ThreadAbortException) { }
            finally
            {
                fs.Close();
            }
        }

        public bool TryToMemory()
        {
            try
            {
                ToMemory();
                return true;
            }
            catch { return false; }
        }

        public void ToDisk()
        {
            if (state == State.Disk) { return; }

            FileStream fs = new FileStream(backingfile, FileMode.Create);
            try
            {
                SurfaceSerializer.Serialize(fs, surface);
                state = State.Disk;
            }
            catch (ThreadAbortException) { }
            finally
            {
                fs.Close();
                surface.Dispose();
            }
        }

        public bool TryToDisk()
        {
            try
            {
                ToDisk();
                return true;
            }
            catch { return false; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            File.Delete(backingfile);
            surface.Dispose();
            state = State.Disposed;
        }

        #endregion

        #region ICloneable Members

        public object Clone()
        {
            return new DiskBackedSurface(this);
        }

        #endregion
    }

    public enum DiskBackedSurfaceState
    {
        Memory,
        Disk,
        Disposed
    }
}