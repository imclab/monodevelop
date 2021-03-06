// 
// ButtonBar.cs
//  
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
// 
// Copyright (c) 2012 Xamarin Inc. (http://xamarin.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using Gtk;
using Gdk;
using System.Collections.Generic;
using MonoDevelop.Components;
using Cairo;
using System.Linq;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Core;

namespace MonoDevelop.Components.MainToolbar
{
	class ButtonBar : EventBox, ICommandBar
	{
		const int SeparatorSpacing = 6;
		List<ButtonBarButton> buttons = new List<ButtonBarButton> ();
		ButtonBarButton[] visibleButtons;

		Xwt.Drawing.Image[] btnNormalOriginal;
		Xwt.Drawing.Image[] btnPressedOriginal;
		Xwt.Drawing.Image[] btnNormal;
		Xwt.Drawing.Image[] btnPressed;

		ButtonBarButton pushedButton;
		object lastCommandTarget;
		int currentImagesHeight;

		public ButtonBar ()
		{
			WidgetFlags |= Gtk.WidgetFlags.AppPaintable;
			VisibleWindow = false;
			Events |= EventMask.ButtonPressMask | EventMask.ButtonReleaseMask;

			btnNormalOriginal = new Xwt.Drawing.Image[] {
				Xwt.Drawing.Image.FromResource ("btn-debug-base-left-cap-normal.png"),
				Xwt.Drawing.Image.FromResource ("btn-debug-base-middle-cap-normal.png"),
				Xwt.Drawing.Image.FromResource ("btn-debug-base-right-cap-normal.png")
			};

			btnPressedOriginal = new Xwt.Drawing.Image[] {
				Xwt.Drawing.Image.FromResource ("btn-debug-base-left-cap-pressed.png"),
				Xwt.Drawing.Image.FromResource ("btn-debug-base-middle-cap-pressed.png"),
				Xwt.Drawing.Image.FromResource ("btn-debug-base-right-cap-pressed.png")
			};
			btnNormal = new Xwt.Drawing.Image[btnNormalOriginal.Length];
			btnPressed = new Xwt.Drawing.Image[btnNormalOriginal.Length];
			HasTooltip = true;
		}

		void ScaleImages (int newHeight)
		{
			if (currentImagesHeight == newHeight)
				return;

			currentImagesHeight = newHeight;
			for (int n=0; n<btnNormalOriginal.Length; n++) {
				if (btnNormal[n] != null)
					btnNormal[n].Dispose ();
				btnNormal[n] = ExpandImageVertically (btnNormalOriginal[n], newHeight);
			}
			for (int n=0; n<btnPressedOriginal.Length; n++) {
				if (btnPressed[n] != null)
					btnPressed[n].Dispose ();
				btnPressed[n] = ExpandImageVertically (btnPressedOriginal[n], newHeight);
			}
		}

		ButtonBarButton[] VisibleButtons {
			get {
				if (visibleButtons == null)
					visibleButtons = buttons.Where (b => b.Visible || b.IsSeparator).ToArray ();
				return visibleButtons;
			}
		}

		StateType leaveState = StateType.Normal;
		protected override bool OnEnterNotifyEvent (EventCrossing evnt)
		{
			State = leaveState;
			return base.OnEnterNotifyEvent (evnt);
		}

		protected override bool OnLeaveNotifyEvent (EventCrossing evnt)
		{
			leaveState = State;
			State = StateType.Normal;
			return base.OnLeaveNotifyEvent (evnt);
		}

		protected override bool OnButtonPressEvent (EventButton evnt)
		{
			if (evnt.Button == 1) {
				pushedButton = VisibleButtons.FirstOrDefault (b => b.Allocation.Contains (Allocation.X + (int)evnt.X, Allocation.Y + (int)evnt.Y));
				if (pushedButton != null && pushedButton.Enabled)
					State = StateType.Selected;
			}
			return true;
		}

		protected override bool OnButtonReleaseEvent (EventButton evnt)
		{
			if (State == StateType.Selected && pushedButton != null)
				IdeApp.CommandService.DispatchCommand (pushedButton.CommandId, null, lastCommandTarget, CommandSource.MainToolbar);
			State = StateType.Prelight;
			leaveState = StateType.Normal;
			pushedButton = null;
			return true;
		}

		protected override bool OnQueryTooltip (int x, int y, bool keyboard_tooltip, Tooltip tooltip)
		{
			var button = VisibleButtons.FirstOrDefault (b => b.Allocation.Contains (Allocation.X + (int)x, Allocation.Y + (int)y));
			if (button != null) {
				tooltip.Text = button.Tooltip;
				var rect = button.Allocation;
				rect.Offset (-Allocation.X, -Allocation.Y);
				tooltip.TipArea = rect;
				return true;
			} else {
				return false;
			}
		}

		public void Clear ()
		{
			buttons.Clear ();
			visibleButtons = null;
			pushedButton = null;
			QueueResize ();
		}

		public void Add (string commandId)
		{
			ButtonBarButton b = new ButtonBarButton (commandId);
			var ci = IdeApp.CommandService.GetCommandInfo (commandId, new CommandTargetRoute (lastCommandTarget));
			if (ci != null)
				UpdateButton (b, ci);
			buttons.Add (b);
			visibleButtons = null;
			QueueResize ();
		}

		public void AddSeparator ()
		{
			buttons.Add (new ButtonBarButton (null));
			visibleButtons = null;
			QueueResize ();
		}

		protected override void OnSizeRequested (ref Requisition requisition)
		{
			base.OnSizeRequested (ref requisition);
			requisition.Width = VisibleButtons.Sum (b => b.Visible ? (!b.IsSeparator ? (int)btnNormalOriginal[0].Width : SeparatorSpacing) : 0);
			requisition.Height = (int)btnNormalOriginal[0].Height;
		}

		protected override bool OnExposeEvent (EventExpose evnt)
		{
			ScaleImages (Allocation.Height);

			using (var context = Gdk.CairoHelper.Create (evnt.Window)) {
				double x = Allocation.X, y = Allocation.Y;
				for (int i = 0; i < VisibleButtons.Length; i++) {
					bool nextIsSeparator = (i < VisibleButtons.Length - 1 && VisibleButtons[i + 1].IsSeparator) || i == visibleButtons.Length - 1;
					bool lastWasSeparator = (i > 0 && VisibleButtons[i - 1].IsSeparator) || i == 0;
					ButtonBarButton button = VisibleButtons [i];
					if (button.IsSeparator) {
						if (!lastWasSeparator)
							x += SeparatorSpacing;
						continue;
					}
					Xwt.Drawing.Image[] images = State == StateType.Selected && pushedButton == button ? btnPressed : btnNormal;
					Xwt.Drawing.Image img = images [lastWasSeparator ? 0 : nextIsSeparator ? 2 : 1];
					context.DrawImage (this, img, x, y);

					button.Allocation = new Gdk.Rectangle ((int)x, (int)y, (int)img.Width, (int)img.Height);

					var icon = ImageService.GetIcon (button.Image, IconSize.Menu);
					if (!Sensitive || !button.Enabled)
						icon = icon.WithAlpha (0.4);
					context.DrawImage (this, icon, Math.Truncate (x + (img.Width - icon.Width) / 2), Math.Truncate (y + (img.Height - icon.Height) / 2));
					x += img.Width;
				}
			}
			return base.OnExposeEvent (evnt);
		}

		Xwt.Drawing.Image ExpandImageVertically (Xwt.Drawing.Image img, int newHeight)
		{
			if (newHeight <= img.Height)
				return img;

			Xwt.Drawing.ImageBuilder ib = new Xwt.Drawing.ImageBuilder (img.Width, newHeight);
			int h1 = (int)img.Height / 2;
			int h2 = (int)img.Height - h1;

			var ctx = ib.Context;
			ctx.Save ();
			ctx.Rectangle (0, 0, img.Width, h1);
			ctx.Clip ();
			ctx.DrawImage (img, 0, 0);
			ctx.Restore ();

			ctx.Save ();
			ctx.Rectangle (0, newHeight - h2, img.Width, h2);
			ctx.Clip ();
			ctx.DrawImage (img, 0, newHeight - img.Height);
			ctx.Restore ();

			ctx.Rectangle (0, h1, img.Width, newHeight - h1 - h2);
			ctx.Clip ();
			ctx.DrawImage (img, 0, 0, img.Width, newHeight);

			return ib.ToVectorImage ();
		}

		public sealed class ClickEventArgs : EventArgs
		{
			public int Button { get; private set; }

			public ClickEventArgs (int button)
			{
				this.Button = button;
			}
		}

		#region ICommandBar implementation
		void ICommandBar.Update (object activeTarget)
		{
			lastCommandTarget = activeTarget;
			foreach (var b in buttons.Where (bu => !bu.IsSeparator)) {
				var ci = IdeApp.CommandService.GetCommandInfo (b.CommandId, new CommandTargetRoute (activeTarget));
				UpdateButton (b, ci);
			}
		}

		void UpdateButton (ButtonBarButton b, CommandInfo ci)
		{
			if (ci.Icon != b.Image) {
				b.Image = ci.Icon;
				QueueDraw ();
			}
			if (ci.Visible != b.Visible) {
				b.Visible = ci.Visible;
				visibleButtons = null;
				QueueResize ();
			}
			if (ci.Enabled != b.Enabled) {
				b.Enabled = ci.Enabled;
				QueueDraw ();
			}
			if (ci.Description != b.Tooltip)
				b.Tooltip = ci.Description;
		}

		void ICommandBar.SetEnabled (bool enabled)
		{
			Sensitive = enabled;
		}
		#endregion
	}

	class ButtonBarButton
	{
		public ButtonBarButton (string commandId)
		{
			this.CommandId = commandId;
		}

		public IconId Image { get; set; }
		public string CommandId { get; set; }
		internal bool Enabled { get; set; }
		internal bool Visible { get; set; }
		public string Tooltip { get; set; }

		public Gdk.Rectangle Allocation;

		public bool IsSeparator {
			get { return CommandId == null; }
		}
	}
}

