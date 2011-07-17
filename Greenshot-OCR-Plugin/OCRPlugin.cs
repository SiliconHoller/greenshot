﻿/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2011  Thomas Braun, Jens Klingen, Robin Krom
 * 
 * For more information see: http://getgreenshot.org/
 * The Greenshot project is hosted on Sourceforge: http://sourceforge.net/projects/greenshot/
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Windows.Forms;

using Greenshot.Interop;
using Greenshot.Plugin;
using GreenshotPlugin.Controls;
using GreenshotPlugin.Core;
//using Microsoft.Win32;

namespace GreenshotOCR {
	/// <summary>
	/// OCR Plugin Greenshot
	/// </summary>
	public class OcrPlugin : IGreenshotPlugin {
		private static log4net.ILog LOG = log4net.LogManager.GetLogger(typeof(OcrPlugin));
		private const string CONFIG_FILENAME = "ocr-config.properties";

		private IGreenshotPluginHost host;
		private ICaptureHost captureHost = null;
		private PluginAttribute myAttributes;
		private OCRConfiguration config;
		private ToolStripMenuItem ocrMenuItem = new ToolStripMenuItem();
		private int hotkeyIdentifier = 0;

		public OcrPlugin() { }

		/// <summary>
		/// Implementation of the IGreenshotPlugin.Initialize
		/// </summary>
		/// <param name="host">Use the IGreenshotPluginHost interface to register events</param>
		/// <param name="captureHost">Use the ICaptureHost interface to register in the MainContextMenu</param>
		/// <param name="pluginAttribute">My own attributes</param>
		public void Initialize(IGreenshotPluginHost host, ICaptureHost captureHost, PluginAttribute myAttributes) {
			LOG.Debug("Initialize called of " + myAttributes.Name);
			this.host = (IGreenshotPluginHost)host;
			this.captureHost = captureHost;
			this.myAttributes = myAttributes;

			if (!HasMODI()) {
				LOG.Warn("No MODI found!");
				return;
			}
			// Load configuration
			config = IniConfig.GetIniSection<OCRConfiguration>();
			
			if (config.Language != null) {
				config.Language = config.Language.Replace("miLANG_","").Replace("_"," ");
			}

			SetHotkeys();

			// Here we can hang ourselves to the main context menu!
			ocrMenuItem.Text = "Region OCR";
			ocrMenuItem.Click += new System.EventHandler(MainMenuClick);

			ContextMenuStrip contextMenu = host.MainMenu;
			bool addedItem = false;

			// Try to find a separator, so we insert ourselves before
			for(int i=0; i < contextMenu.Items.Count; i++) {
				if (contextMenu.Items[i].GetType() == typeof(ToolStripSeparator)) {
					contextMenu.Items.Insert(i, ocrMenuItem);
					addedItem = true;
					break;
				}
			}
			// If we didn't insert the item, we just add it...
			if (!addedItem) {
				contextMenu.Items.Add(ocrMenuItem);
			}
		}
		
		/// <summary>
		/// Implementation of the IGreenshotPlugin.Shutdown
		/// </summary>
		public void Shutdown() {
			LOG.Debug("Shutdown of " + myAttributes.Name);
			HotkeyControl.UnregisterHotkey(hotkeyIdentifier);
			hotkeyIdentifier = 0;
		}
		
		private void SetHotkeys() {
			if (hotkeyIdentifier > 0) {
				HotkeyControl.UnregisterHotkey(hotkeyIdentifier);
				hotkeyIdentifier = 0;
			}
			hotkeyIdentifier = HotkeyControl.RegisterHotKey(config.HotKey, new HotKeyHandler(MyHotkeyHandler));
			if (hotkeyIdentifier > 0) {
				ocrMenuItem.ShortcutKeyDisplayString = HotkeyControl.GetLocalizedHotkeyStringFromString(config.HotKey);
			} else {
				ocrMenuItem.ShortcutKeyDisplayString = "";
			}
		}

		/// <summary>
		/// Implementation of the IPlugin.Configure
		/// </summary>
		public virtual void Configure() {
			if (!HasMODI()) {
				MessageBox.Show("Sorry, is seems that Microsoft Office Document Imaging (MODI) is not installed, therefor the OCR Plugin cannot work.");
				return;
			}
			SettingsForm settingsForm = new SettingsForm(Enum.GetNames(typeof(ModiLanguage)), config);
			DialogResult result = settingsForm.ShowDialog();
			if (result == DialogResult.OK) {
				// "Re"set hotkeys
				SetHotkeys();
				IniConfig.Save();
			}
		}

		private void StartOCRRegion() {
			LOG.Debug("Starting OCR!");
			captureHost.MakeCapture(CaptureMode.Region, false, new CaptureHandler(DoOCR));
		}
		
		private void MyHotkeyHandler() {
			StartOCRRegion();
		}
		/// <summary>
		/// Is called when the OCR menu is selected
		/// </summary>
		/// <param name="sender">ContextMenu</param>
		/// <param name="e">EventArgs from ContextMenu</param>
		private void MainMenuClick(object sender, EventArgs e) {
			StartOCRRegion();
		}

		/// <summary>
		/// Handling of the CaptureTaken "event" from the ICaptureHost
		/// We do the OCR here!
		/// </summary>
		/// <param name="ImageOutputEventArgs">Has the Image and the capture details</param>
		private const int MIN_WIDTH = 130;
		private const int MIN_HEIGHT = 130;
		private void DoOCR(object sender, CaptureTakenEventArgs eventArgs) {
			if (eventArgs.Capture.Image == null) {
				return;
			}
			string file = host.GetFilename(OutputFormat.bmp, eventArgs.Capture.CaptureDetails);
			string filePath = Path.Combine(Path.GetTempPath(),file);
			
			using (FileStream stream = File.Create(filePath)) {
				Image capturedImage = eventArgs.Capture.Image;
				if (capturedImage.Width < MIN_WIDTH || capturedImage.Height < MIN_HEIGHT) {
					LOG.Debug("Captured image is not big enough for OCR, growing image...");
					int newWidth = Math.Max(capturedImage.Width, MIN_WIDTH);
					int newHeight = Math.Max(capturedImage.Height, MIN_HEIGHT);
					using (Image tmpImage = new Bitmap(newWidth, newHeight, capturedImage.PixelFormat)) {
						using (Graphics graphics = Graphics.FromImage(tmpImage)) {
							graphics.Clear(Color.White);
							graphics.DrawImage(capturedImage, Point.Empty);
						}
						host.SaveToStream(tmpImage, stream, OutputFormat.bmp, 100);
					}
				} else {
					host.SaveToStream(capturedImage, stream, OutputFormat.bmp, 100);
				}
			}
		
			LOG.Debug("Saved tmp file to: " + filePath);

			string text = "";
			try {
				using (ModiDocu modiDocument = (ModiDocu)COMWrapper.GetOrCreateInstance(typeof(ModiDocu))) {
					modiDocument.Create(filePath);
					modiDocument.OCR((ModiLanguage)Enum.Parse(typeof(ModiLanguage), config.Language), config.Orientimage, config.StraightenImage);
					IImage modiImage = modiDocument.Images[0];
					ILayout layout = modiImage.Layout;
					text = layout.Text;
					modiDocument.Close(false);
				}
			} catch (Exception e) {
				LOG.Error("Error while calling Microsoft Office Document Imaging (MODI) to OCR: ", e);
			} finally {
				if (File.Exists(filePath)) {
					LOG.Debug("Cleaning up tmp file: " + filePath);
					File.Delete(filePath);
				}
			}
			if (text == null || text.Trim().Length == 0) {
				return;
			}
				
			try {
				LOG.DebugFormat("Pasting OCR Text to Clipboard: {0}", text);
				// Paste to Clipboard (the Plugin currently doesn't have access to the ClipboardHelper from Greenshot
				IDataObject ido = new DataObject();
				ido.SetData(DataFormats.Text, true, text);
				Clipboard.SetDataObject(ido, true);
			} catch (Exception e) {
				LOG.Error("Problem pasting text to clipboard: ", e);
			}
		}

		private bool HasMODI() {
			try {
				using (ModiDocu modiDocument = (ModiDocu)COMWrapper.GetOrCreateInstance(typeof(ModiDocu))) {
					modiDocument.Close(false);
				}
				return true;
			} catch(Exception e) {
				LOG.Debug("Error trying to initiate MODI:", e);
			}
			LOG.InfoFormat("No Microsoft Office Document Imaging (MODI) found, disabling OCR");
			return false;
		}
	}
}