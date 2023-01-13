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
        private State state;
        private Surface surface;
        private int width;
        private int height;

        private void Initialize()
        {
            width = surface.Width;
            height = surface.Height;
            backingfile = Path.GetTempFileName();
            state = State.Memory;
        }

        public DiskBackedSurface(int width, int height)
        {
            surface = new Surface(width, height);
            Initialize();
        }

        public DiskBackedSurface(Size size)
        {
            surface = new Surface(size);
            Initialize();
        }

        public DiskBackedSurface(Surface surface, bool takeownership)
        {
            if (takeownership)
            {
                this.surface = surface;
            }
            else
            {
                this.surface = surface.Clone();
            }
            Initialize();
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
            DiskBackedSurface retval = new DiskBackedSurface(this.surface, true);
            retval.state = this.state;
            retval.backingfile = this.backingfile;
            return retval;
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