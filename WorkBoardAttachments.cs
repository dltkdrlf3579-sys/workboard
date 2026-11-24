using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

public class JobAttachment
{
    public string Name;
    public byte[] Data;

    public static JobAttachment Read(string path)
    {
        return new JobAttachment { Name=Path.GetFileName(path), Data=File.ReadAllBytes(path) };
    }

    public bool IsMail()
    {
        var ext=Path.GetExtension(Name);
        return String.Equals(ext,".eml",StringComparison.OrdinalIgnoreCase)
            || String.Equals(ext,".msg",StringComparison.OrdinalIgnoreCase);
    }

    public void SaveAs(string path)
    {
        string temp=Path.Combine(Path.GetDirectoryName(path),Guid.NewGuid().ToString("N")+".tmp");
        try {
            using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                output.Write(Data,0,Data.Length);output.Flush(true);
            }
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        finally { if(File.Exists(temp))File.Delete(temp); }
    }
}

internal static class MailPreview
{
    static readonly string folder=Path.Combine(Path.GetTempPath(),"PersonalWorkBoard-Mail");
    static bool started;

    static void Clean()
    {
        try {
            foreach(var file in Directory.GetFiles(folder)){
                Guid id;
                var ext=Path.GetExtension(file);
                if((ext!=".eml" && ext!=".msg") || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(file),"N",out id))continue;
                try { File.Delete(file); } catch(IOException) { } catch(UnauthorizedAccessException) { }
            }
        } catch(IOException) { } catch(UnauthorizedAccessException) { }
    }

    public static void Open(JobAttachment attachment)
    {
        if(!attachment.IsMail())return;
        Directory.CreateDirectory(folder);
        if(!started){
            Clean();
            Application.Current.Exit+=(s,e)=>Clean();
            started=true;
        }
        string path=Path.Combine(folder,Guid.NewGuid().ToString("N")+Path.GetExtension(attachment.Name).ToLowerInvariant());
        attachment.SaveAs(path);
        try { Process.Start(new ProcessStartInfo(path){ UseShellExecute=true, Verb="open" }); }
        catch(System.ComponentModel.Win32Exception){
            File.Delete(path);
            throw new InvalidOperationException("메일을 열 수 없습니다. 메일 파일에 연결된 앱을 확인하거나 ‘다른 이름으로 저장’을 사용하세요.");
        }
    }
}

public partial class Board
{
    // The list belongs to this editor until the user saves the job.
    static void AttachmentControls(Window owner,DockPanel article,List<JobAttachment> draft,bool readOnly,Action changed)
    {
        var heading=new DockPanel { Margin=new Thickness(0,0,0,6) };
        DockTop(article,heading);
        var files=new StackPanel();
        var list=new ScrollViewer { Content=files,MaxHeight=84,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled };
        DockPanel.SetDock(list,Dock.Bottom);article.Children.Add(list);
        Action refresh=null;
        if(!readOnly){
            var add=Quiet("＋ 파일 첨부",()=>{
                var dialog=new OpenFileDialog{Title="첨부할 메일 또는 파일 선택",Multiselect=true,
                    Filter="모든 파일 (*.*)|*.*|메일 파일 (*.eml, *.msg)|*.eml;*.msg"};
                if(dialog.ShowDialog(owner)!=true)return;
                var added=dialog.FileNames.Select(JobAttachment.Read).ToList();
                draft.AddRange(added);changed();refresh();
            });
            add.ToolTip="저장한 메일(.eml, .msg)이나 파일을 첨부합니다.";
            DockPanel.SetDock(add,Dock.Right);heading.Children.Add(add);
        }
        heading.Children.Add(Text("업무 내용",14,ink,true));
        refresh=()=>{
            files.Children.Clear();
            list.Visibility=draft.Count==0 ? Visibility.Collapsed : Visibility.Visible;
            foreach(var item in draft){
                var row=new DockPanel { Margin=new Thickness(0,2,0,0) };files.Children.Add(row);
                var actions=Line();DockPanel.SetDock(actions,Dock.Right);row.Children.Add(actions);
                if(item.IsMail())actions.Children.Add(Quiet("열기",()=>MailPreview.Open(item)));
                actions.Children.Add(Quiet("다른 이름으로 저장",()=>{
                    var dialog=new SaveFileDialog { FileName=Path.GetFileName(item.Name),AddExtension=false,Filter="모든 파일 (*.*)|*.*" };
                    if(dialog.ShowDialog(owner)==true)item.SaveAs(dialog.FileName);
                }));
                if(!readOnly)actions.Children.Add(Quiet("제거",()=>{draft.Remove(item);changed();refresh();}));
                var name=Text(item.Name,13,ink);name.ToolTip=item.Name;row.Children.Add(name);
            }
        };
        refresh();
    }
}

