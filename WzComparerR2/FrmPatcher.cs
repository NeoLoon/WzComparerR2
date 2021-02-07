using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DevComponents.DotNetBar;
using DevComponents.AdvTree;
using System.IO;
using System.Linq;

using System.Threading;
using WzComparerR2.WzLib;
using WzComparerR2.Patcher;
using WzComparerR2.Comparer;
using WzComparerR2.Config;

namespace WzComparerR2
{
    public partial class FrmPatcher : DevComponents.DotNetBar.Office2007Form
    {
        public FrmPatcher()
        {
            InitializeComponent();
            panelEx1.AutoScroll = true;

            var settings = WcR2Config.Default.PatcherSettings;
            if (settings.Count <= 0)
            {
                settings.Add(new PatcherSetting("KMST", "http://maplestory.dn.nexoncdn.co.kr/PatchT/{1:d5}/{0:d5}to{1:d5}.patch"));
                settings.Add(new PatcherSetting("KMS", "http://maplestory.dn.nexoncdn.co.kr/Patch/{1:d5}/{0:d5}to{1:d5}.patch"));
                settings.Add(new PatcherSetting("JMS", "http://webdown2.nexon.co.jp/maple/patch/patchdir/{1:d5}/{0:d5}to{1:d5}.patch"));
                settings.Add(new PatcherSetting("GMS", "http://download2.nexon.net/Game/MapleStory/patch/patchdir/{1:d5}/CustomPatch{0}to{1}.exe"));
                settings.Add(new PatcherSetting("TMS", "http://tw.cdnpatch.maplestory.beanfun.com/maplestory/patch/patchdir/{1:d5}/{0:d5}to{1:d5}.patch"));
                settings.Add(new PatcherSetting("MSEA", "http://patch.maplesea.com/sea/patch/patchdir/{1:d5}/{0:d5}to{1:d5}.patch"));
                settings.Add(new PatcherSetting("CMS", "http://mxd.clientdown.sdo.com/maplestory/patch/patchdir/{1:d5}/{0:d5}to{1:d5}.patch"));
            }

            foreach (PatcherSetting p in settings)
            {
                comboBoxEx1.Items.Add(p);
            }
            if (comboBoxEx1.Items.Count > 0)
                comboBoxEx1.SelectedIndex = 0;

            foreach (WzPngComparison comp in Enum.GetValues(typeof(WzPngComparison)))
            {
                cmbComparePng.Items.Add(comp);
            }
            cmbComparePng.SelectedItem = WzPngComparison.SizeAndDataLength;
            typedParts = Enum.GetValues(typeof(Wz_Type)).Cast<Wz_Type>().ToDictionary(type => type, type => new List<PatchPartContext>());
        }

        Thread patchThread;
        EventWaitHandle waitHandle;
        bool waiting;

        private void combineUrl()
        {
            PatcherSetting p = comboBoxEx1.SelectedItem as PatcherSetting;
            if (p != null)
                txtUrl.Text = p.Url;
        }

        private void comboBoxEx1_SelectedIndexChanged(object sender, EventArgs e)
        {
            PatcherSetting p = comboBoxEx1.SelectedItem as PatcherSetting;
            if (p != null)
            {
                integerInput1.Value = p.Version0;
                integerInput2.Value = p.Version1;
                combineUrl();
            }
        }

        private void integerInput1_ValueChanged(object sender, EventArgs e)
        {
            PatcherSetting p = comboBoxEx1.SelectedItem as PatcherSetting;
            if (p != null)
            {
                p.Version0 = integerInput1.Value;
                combineUrl();
            }
        }

        private void integerInput2_ValueChanged(object sender, EventArgs e)
        {
            PatcherSetting p = comboBoxEx1.SelectedItem as PatcherSetting;
            if (p != null)
            {
                p.Version1 = integerInput2.Value;
                combineUrl();
            }
        }

        private void buttonXCheck_Click(object sender, EventArgs e)
        {
            DownloadingItem item = new DownloadingItem(txtUrl.Text, null);
            try
            {
                item.GetFileLength();
                if (item.FileLength > 0)
                {
                    MessageBoxEx.Show(string.Format("Size : {0:N0}B, Uploaded Date : {1:yyyy-MM-dd HH:mm:ss}", item.FileLength, item.LastModified));
                }
                else
                {
                    MessageBoxEx.Show("File does not exist.");
                }
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show("Error : " + ex.Message);
            }
        }

        private void FrmPatcher_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (patchThread != null && patchThread.IsAlive)
            {
                patchThread.Abort();
            }
            ConfigManager.Reload();
            WcR2Config.Default.PatcherSettings.Clear();
            foreach (PatcherSetting item in comboBoxEx1.Items)
            {
                WcR2Config.Default.PatcherSettings.Add(item);
            }
            ConfigManager.Save();
        }

        private void NewFile(BinaryReader reader, string fileName, string patchDir)
        {
            string tmpFile = Path.Combine(patchDir, fileName);
            string dir = Path.GetDirectoryName(tmpFile);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private void buttonXOpen1_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = "Open Patch File";
            dlg.Filter = "Patch File (*.patch;*.exe)|*.patch;*.exe";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                txtPatchFile.Text = dlg.FileName;
            }
        }

        private void buttonXOpen2_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.Description = "Please Select the MapleStory folder.";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                txtMSFolder.Text = dlg.SelectedPath;
            }
        }

        private void buttonXPatch_Click(object sender, EventArgs e)
        {
            if (patchThread != null)
            {
                if (waiting)
                {
                    waitHandle.Set();
                    waiting = false;
                    return;
                }
                else
                {
                    MessageBoxEx.Show("There is a ongoing patch");
                    return;
                }
            }
            compareFolder = null;
            if (chkCompare.Checked)
            {
                FolderBrowserDialog dlg = new FolderBrowserDialog();
                dlg.Description = "Please select the folder to store save compared result.";
                if (dlg.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
                compareFolder = dlg.SelectedPath;
            }

            patchFile = txtPatchFile.Text;
            msFolder = txtMSFolder.Text;
            prePatch = chkPrePatch.Checked;
            deadPatch = chkDeadPatch.Checked;

            patchThread = new Thread(() => ExecutePatch(patchFile, msFolder, prePatch));
            patchThread.Priority = ThreadPriority.Highest;
            waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            waiting = false;
            patchThread.Start();
            panelEx2.Visible = true;
            expandablePanel2.Height = 340;
        }

        string patchFile;
        string msFolder;
        string compareFolder;
        bool prePatch ;
        bool deadPatch;
        string htmlFilePath;
        FileStream htmlFile;
        StreamWriter sw;
        Dictionary<Wz_Type, List<PatchPartContext>> typedParts;

        private void ExecutePatch(string patchFile, string msFolder, bool prePatch)
        {
            WzPatcher patcher = null;
            advTreePatchFiles.Nodes.Clear();
            txtNotice.Clear();
            txtPatchState.Clear();
            try
            {
                patcher = new WzPatcher(patchFile);
                patcher.PatchingStateChanged += new EventHandler<PatchingEventArgs>(patcher_PatchingStateChanged);
                AppendStateText("Verifying Patch... ");
                patcher.OpenDecompress();
                AppendStateText("Complete\r\n");
                AppendStateText("Preparing Patch... \r\n");
                long decompressedSize = patcher.PrePatch();
                AppendStateText("Complete\r\n");
                AppendStateText(string.Format("Patch Size : {0:N0}B...\r\n", decompressedSize));
                AppendStateText(string.Format("Number of files to patch : {0}...\r\n",
                    patcher.PatchParts == null ? -1 : patcher.PatchParts.Count));
                txtNotice.Text = patcher.NoticeText;
                foreach (PatchPartContext part in patcher.PatchParts)
                {
                    advTreePatchFiles.Nodes.Add(CreateFileNode(part));
                    advTreePatchFiles.Nodes[advTreePatchFiles.Nodes.Count - 1].Enabled = prePatch;
                    if (prePatch && part.Type == 1)
                    {
                        advTreePatchFiles.Nodes[advTreePatchFiles.Nodes.Count - 1].Checked = File.Exists(Path.Combine(msFolder, part.FileName));
                    }
                }
                if (prePatch)
                {
                    //advTreePatchFiles.Enabled = true;
                    AppendStateText("Please select files that you want to patch and press Patch button...\r\n");
                    waiting = true;
                    waitHandle.WaitOne();
                    //advTreePatchFiles.Enabled = false;
                    patcher.PatchParts.Clear();
                    for (int i = 0, j = advTreePatchFiles.Nodes.Count; i < j; i++)
                    {
                        if (advTreePatchFiles.Nodes[i].Checked)
                        {
                            patcher.PatchParts.Add(advTreePatchFiles.Nodes[i].Tag as PatchPartContext);
                        }
                        advTreePatchFiles.Nodes[i].Enabled = false;
                    }
                    patcher.PatchParts.Sort((part1, part2) => part1.Offset.CompareTo(part2.Offset));
                }
                AppendStateText("Patching...\r\n");
                DateTime time = DateTime.Now;
                patcher.Patch(msFolder);
                if (!string.IsNullOrEmpty(this.compareFolder))
                {
                    sw.WriteLine("</table>");
                    sw.WriteLine("</p>");

                    //html结束
                    sw.WriteLine("</body>");
                    sw.WriteLine("</html>");

                    try
                    {
                        if (sw != null)
                        {
                            sw.Flush();
                            sw.Close();
                        }
                    }
                    catch
                    {
                    }
                }
                TimeSpan interval = DateTime.Now - time;
                MessageBoxEx.Show(this, "Patch completed: Time elapsed" + interval.ToString(), "Patcher");
            }
            catch (ThreadAbortException)
            {
                MessageBoxEx.Show("Patch was interrupted.", "Error");
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show(this, ex.ToString(), "Error");
            }
            finally
            {
                try
                {
                    if (sw != null)
                    {
                        sw.Flush();
                        sw.Close();
                    }
                }
                catch
                {
                }
                try
                {
                    if (htmlFile != null)
                    {
                        htmlFile.Flush();
                        htmlFile.Close();
                    }
                }
                catch
                {
                }
                htmlFilePath = null;
                foreach (List<PatchPartContext> parts in typedParts.Values)
                {
                    parts.Clear();
                }

                if (patcher != null)
                {
                    patcher.Close();
                    patcher = null;
                }
                patchThread = null;
                waitHandle = null;
                GC.Collect();

                panelEx2.Visible = false;
                expandablePanel2.Height = 157;
            }
        }

        private void patcher_PatchingStateChanged(object sender, PatchingEventArgs e)
        {
            switch (e.State)
            {
                case PatchingState.PatchStart:
                    AppendStateText("[" + e.Part.FileName + "] Patching\r\n");
                    break;
                case PatchingState.VerifyOldChecksumBegin:
                    AppendStateText("  Checking for PrePatch checksum...");
                    progressBarX1.Maximum = e.Part.NewFileLength;
                    break;
                case PatchingState.VerifyOldChecksumEnd:
                    AppendStateText("  Complete\r\n");
                    break;
                case PatchingState.VerifyNewChecksumBegin:
                    AppendStateText("  Checking for PostPatch checksum...");
                    break;
                case PatchingState.VerifyNewChecksumEnd:
                    AppendStateText("  Complete\r\n");
                    break;
                case PatchingState.TempFileCreated:
                    AppendStateText("  Writing Temporary Files...\r\n");
                    progressBarX1.Maximum = e.Part.NewFileLength;
                    break;
                case PatchingState.TempFileBuildProcessChanged:
                    progressBarX1.Value = (int)e.CurrentFileLength;
                    progressBarX1.Text = string.Format("{0:N0}/{1:N0}", e.CurrentFileLength, e.Part.NewFileLength);
                    break;
                case PatchingState.TempFileClosed:
                    AppendStateText("   Writing Complete...\r\n");
                    progressBarX1.Value = 0;
                    progressBarX1.Maximum = 0;
                    progressBarX1.Text = string.Empty;

                    typedParts[e.Part.WzType].Add(e.Part);

                    if (!string.IsNullOrEmpty(this.compareFolder)
                        //&& e.Part.Type == 1
                        && Path.GetExtension(e.Part.FileName).Equals(".wz", StringComparison.OrdinalIgnoreCase)
                        && !Path.GetFileName(e.Part.FileName).Equals("list.wz", StringComparison.OrdinalIgnoreCase)
                        && typedParts[e.Part.WzType].Count == ((WzPatcher)sender).PatchParts.Where(part => part.WzType == e.Part.WzType).Count())
                    {
                        Wz_Structure wznew = new Wz_Structure();
                        Wz_Structure wzold = new Wz_Structure();
                        try
                        {
                            AppendStateText("  파일 비교...\r\n");
                            EasyComparer comparer = new EasyComparer();
                            comparer.OutputPng = chkOutputPng.Checked;
                            comparer.OutputAddedImg = chkOutputAddedImg.Checked;
                            comparer.OutputRemovedImg = chkOutputRemovedImg.Checked;
                            comparer.Comparer.PngComparison = (WzPngComparison)cmbComparePng.SelectedItem;
                            comparer.Comparer.ResolvePngLink = chkResolvePngLink.Checked;
                            comparer.PatchingStateChanged += new EventHandler<PatchingEventArgs>(patcher_PatchingStateChanged);
                            //wznew.Load(e.Part.TempFilePath, false);
                            //wzold.Load(e.Part.OldFilePath, false);
                            //comparer.EasyCompareWzFiles(wznew.wz_files[0], wzold.wz_files[0], this.compareFolder);
                            foreach (PatchPartContext part in typedParts[e.Part.WzType])
                            {
                                if (part.Type != 2)
                                {
                                    wznew.Load(part.TempFilePath, false);
                                }
                                if (part.Type != 0)
                                {
                                    wzold.Load(part.OldFilePath, false);
                                }
                            }
                            if (htmlFilePath == null)
                            {
                                htmlFilePath = Path.Combine(this.compareFolder, "index.html");

                                htmlFile = new FileStream(htmlFilePath, FileMode.Create, FileAccess.Write);
                                sw = new StreamWriter(htmlFile, Encoding.UTF8);
                                sw.WriteLine("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\">");
                                sw.WriteLine("<html>");
                                sw.WriteLine("<head>");
                                sw.WriteLine("<meta http-equiv=\"content-type\" content=\"text/html;charset=utf-8\">");
                                sw.WriteLine("<title>Index {0}←{1}</title>", wznew.wz_files.Where(wz_file => wz_file != null).First().Header.WzVersion, wzold.wz_files.Where(wz_file => wz_file != null).First().Header.WzVersion);
                                sw.WriteLine("<link type=\"text/css\" rel=\"stylesheet\" href=\"style.css\" />");
                                sw.WriteLine("</head>");
                                sw.WriteLine("<body>");
                                //输出概况
                                sw.WriteLine("<p class=\"wzf\">");
                                sw.WriteLine("<table>");
                                sw.WriteLine("<tr><th>File Name</th><th>Size of New Version</th><th>Size of Previous Version</th><th>Modified</th><th>Added</th><th>Removed</th></tr>");
                            }
                            comparer.EasyCompareWzStructures(wznew, wzold, this.compareFolder, sw);
                        }
                        catch (Exception ex)
                        {
                            txtPatchState.AppendText(ex.ToString());
                        }
                        finally
                        {
                            wznew.Clear();
                            wzold.Clear();
                            GC.Collect();
                        }

                        if (this.deadPatch && typedParts[e.Part.WzType].Count == ((WzPatcher)sender).PatchParts.Where(part => part.WzType == e.Part.WzType).Count())
                        {
                            foreach (PatchPartContext part in typedParts[e.Part.WzType].Where(part => part.Type == 1))
                            {
                                ((WzPatcher)sender).SafeMove(part.TempFilePath, part.OldFilePath);
                            }
                            AppendStateText("  Applying patch to the file...\r\n");
                        }
                    }

                    if (string.IsNullOrEmpty(this.compareFolder) && this.deadPatch && e.Part.Type == 1)
                    {
                        ((WzPatcher)sender).SafeMove(e.Part.TempFilePath, e.Part.OldFilePath);
                        AppendStateText("  Applying patch to the file...\r\n");
                    }
                    break;
                case PatchingState.CompareStarted:
                    progressBarX1.Maximum = e.Part.NewFileLength;
                    break;
                case PatchingState.CompareProcessChanged:
                    progressBarX1.Value = (int)e.CurrentFileLength;
                    progressBarX1.Text = string.Format("{0:N0}/{1:N0}", e.CurrentFileLength, e.Part.NewFileLength);
                    break;
                case PatchingState.CompareFinished:
                    progressBarX1.Value = 0;
                    progressBarX1.Maximum = 0;
                    progressBarX1.Text = string.Empty;
                    break;
            }
        }

        private void AppendStateText(string text)
        {
            this.Invoke((Action<string>)(t => this.txtPatchState.AppendText(t)), text);
        }

        private Node CreateFileNode(PatchPartContext part)
        {
            Node node = new Node(part.FileName) { CheckBoxVisible = true, Checked = true };
            ElementStyle style = new ElementStyle();
            style.TextAlignment = eStyleTextAlignment.Far;
            switch (part.Type)
            {
                case 0: node.Cells.Add(new Cell("Added", style)); break;
                case 1: node.Cells.Add(new Cell("Modified", style)); break;
                case 2: node.Cells.Add(new Cell("Removed", style)); break;
                default: node.Cells.Add(new Cell(part.Type.ToString(), style)); break;
            }
            node.Cells.Add(new Cell(part.NewFileLength.ToString("n0"), style));
            node.Cells.Add(new Cell(part.NewChecksum.ToString("x8"), style));
            node.Cells.Add(new Cell(part.OldChecksum.ToString("x8"), style));
            if (part.Type == 1)
            {
                node.Cells.Add(new Cell(part.Action0 + "|" + part.Action1 + "|" + part.Action2, style));
            }
            node.Tag = part;
            return node;
        }

        /// <summary>
        /// ForTestOnly
        /// </summary>
        private void buttonX1_Click(object sender, EventArgs e)
        {
            WzPatcher patcher = new WzPatcher(@"F:\TDDOWNLOAD\Anime\00475to00476.patch");
            patcher.OpenDecompress();
            patcher.PrePatch();
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (PatchPartContext part in patcher.PatchParts)
            {
                if (part.FileName.Equals("map.wz", StringComparison.OrdinalIgnoreCase))
                {
                    patcher.RebuildFile(part, @"E:\", @"E:\MapleT");
                    break;
                }
            }
            sw.Stop();
            MessageBoxEx.Show(sw.ElapsedMilliseconds.ToString());
            patcher.Close();
        }

        private void buttonXOpen3_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = "Open Patch File";
            dlg.Filter = "Patch File (*.patch;*.exe)|*.patch;*.exe";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                txtPatchFile2.Text = dlg.FileName;
            }
        }

        private void buttonXOpen4_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.Description = "Please Select the MapleStory folder.";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                txtMSFolder2.Text = dlg.SelectedPath;
            }
        }

        private void buttonXCreate_Click(object sender, EventArgs e)
        {
            MessageBoxEx.Show(@"> 这是一个测试功能...
> 还没完成 所以请选择patch文件  exe补丁暂时懒得分离
> 没有检查原客户端版本 为了正确执行请预先确认
> 暂时不提供文件块的筛选或文件缺失提示
> 没优化 于是可能生成文件体积较大 但是几乎可以保证完整性", "声明");

            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Patch File (*.patch)|*.patch";
            dlg.Title = "Save Patch File";
            dlg.CheckFileExists = false;
            dlg.InitialDirectory = Path.GetDirectoryName(txtPatchFile2.Text);
            dlg.FileName = Path.GetFileNameWithoutExtension(txtPatchFile2.Text) + "_reverse.patch";

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ReversePatcherBuilder builder = new ReversePatcherBuilder();
                    builder.msDir = txtMSFolder2.Text;
                    builder.patchFileName = txtPatchFile2.Text;
                    builder.outputFileName = dlg.FileName;
                    builder.Build();
                }
                catch(Exception ex)
                {
                }
            }
        }

        private void FrmPatcher_Load(object sender, EventArgs e)
        {

        }
    }
}