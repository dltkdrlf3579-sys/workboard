using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

public class Job {
 public string Title="",Who="",Received="",Due="",State="진행중",Memo="",Finished="";
 public byte[] Body;
 public bool Trash;
 public Job Copy(){return (Job)MemberwiseClone();}
}
public partial class Board : Window {
 static readonly string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"업무대장.xml");
 static XmlSerializer serializer=new XmlSerializer(typeof(List<Job>));
 List<Job> jobs=new List<Job>();
 StackPanel rows=new StackPanel(), tabs=new StackPanel{Orientation=Orientation.Horizontal};
 TextBox start=new TextBox{Width=105}, end=new TextBox{Width=105};
 string mode="미완료"; DateTime? from,to; bool byDue;
 HashSet<Job> picks=new HashSet<Job>(); Button export;
 static string Day(DateTime d){return d.ToString("yyyy-MM-dd");}
 static DateTime? Date(string s,int year){
 DateTime d; s=(s??"").Trim().Replace('/','-').Replace('.','-');
 if(s.Split('-').Length==2)s=year+"-"+s;
 return DateTime.TryParseExact(s,new[]{"yyyy-M-d","yyyy-MM-dd","yyyy-M-dd","yyyy-MM-d"},CultureInfo.InvariantCulture,DateTimeStyles.None,out d)?d:(DateTime?)null;}
 static string DueValue(string raw,int year){
     raw=(raw??"").Trim();var date=Date(raw,year);return date.HasValue ? Day(date.Value) : raw;
 }
 static void SetStatus(Job job,string state){
     job.State=state;
     if(state!="완료")job.Finished="";
     else if(String.IsNullOrEmpty(job.Finished))job.Finished=Day(DateTime.Today);
 }
 static readonly string[] States={"진행중","회신대기","완료"};
 internal event EventHandler JobsChanged;
 internal int UnfinishedCount { get { return jobs.Count(j=>!j.Trash && j.State!="완료"); } }
 void Save(List<Job> next, bool purge=false){
 string temp=file+".tmp"; using(var stream=File.Create(temp)){serializer.Serialize(stream,next);stream.Flush(true);}
 if(purge)File.Delete(file+".bak");
 if(File.Exists(file))File.Replace(temp,file,purge?null:file+".bak");else File.Move(temp,file); jobs=next;
 if(JobsChanged!=null)JobsChanged(this,EventArgs.Empty);
 }
 void Update(Job old,Job j){var next=jobs.ToList();if(old==null)next.Insert(0,j);else next[next.IndexOf(old)]=j;Save(next);if(old!=null && picks.Remove(old) && !j.Trash)picks.Add(j);Draw();}
 public Board(){
 if(File.Exists(file))using(var f=File.OpenRead(file))jobs=(List<Job>)serializer.Deserialize(f);
 BuildBoard();
 }
 void Filter(){var a=Date(start.Text,DateTime.Today.Year);var b=Date(end.Text,a.HasValue?a.Value.Year:DateTime.Today.Year);
 if((start.Text.Trim()!=""&&!a.HasValue)||(end.Text.Trim()!=""&&!b.HasValue)||a>b){MessageBox.Show("접수일을 11/23 또는 2026-11-23 형식으로 입력하고 기간을 확인하세요.");return;}from=a;to=b??a;picks.Clear();Draw();}
 List<Job> Visible(){var q=jobs.Where(j=>j.Trash==(mode=="휴지통")&&(mode!="미완료"||j.State!="완료")&&(mode!="완료"||j.State=="완료")&&(!from.HasValue||String.CompareOrdinal(j.Received,Day(from.Value))>=0)&&(!to.HasValue||String.CompareOrdinal(j.Received,Day(to.Value))<=0));
 return (byDue?q.OrderBy(j=>Date(j.Due,DateTime.Today.Year)??DateTime.MaxValue):q.OrderByDescending(j=>j.Received)).ToList();}
 static MenuItem Option(string text,Action run){
     var item=new MenuItem{Header=text};
     item.Click+=(o,e)=>{try{run();}catch(Exception ex){MessageBox.Show(ex.Message);}};
     return item;
 }
 bool MoveTrash(Job j){
     if(!j.Trash && MessageBox.Show("‘"+j.Title+"’을 휴지통으로 옮길까요?","휴지통",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return false;
     var next=j.Copy();next.Trash=!j.Trash;Update(j,next);return true;
 }
 bool DeleteForever(Job j){
     if(!j.Trash || MessageBox.Show("‘"+j.Title+"’을 영구 삭제할까요? 본문과 캡처도 삭제됩니다.","영구 삭제",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return false;
     Save(jobs.Where(item=>item!=j).ToList(),true);Draw();return true;
 }
 ContextMenu RowMenu(Job j){
     var menu=new ContextMenu();
     if(j.Trash){
         menu.Items.Add(Option("복원",()=>MoveTrash(j)));
         menu.Items.Add(Option("영구 삭제",()=>DeleteForever(j)));
     }else{
         var states=new MenuItem{Header="상태 변경"};
         foreach(string state in States){
             var item=Option(state,()=>{var next=j.Copy();SetStatus(next,state);Update(j,next);});
             item.IsEnabled=state!=j.State;states.Items.Add(item);
         }
         menu.Items.Add(states);menu.Items.Add(Option("업무명 복사",()=>Clipboard.SetText(j.Title)));
         menu.Items.Add(new Separator());menu.Items.Add(Option("휴지통으로 이동",()=>MoveTrash(j)));
     }
     return menu;
 }
 static Button Danger(string caption, Action action){
     var button=Quiet(caption,action);button.Foreground=Paint("#A33F46");return button;
 }
 static Button FooterAction(string caption, Action action){
     var button=Btn(caption,action,true);button.MinWidth=80;button.Margin=new Thickness(8,0,0,0);return button;
 }
 static TextBox ArticleInput(string name, string value, string hint){
     var input=new TextBox { Text=value, Tag=hint, Style=(Style)theme["ArticleInput"] };
     System.Windows.Automation.AutomationProperties.SetName(input,name);
     return input;
 }
 static TextBox Metadata(Panel row,string caption,string value,string hint){
     var group=new StackPanel { Margin=new Thickness(0,0,12,7) };
     var label=Text(caption,14,ink,true); label.Margin=new Thickness(0,0,0,5);
     group.Children.Add(label);
     var input=ArticleInput(caption,value,hint);
     group.Children.Add(input);row.Children.Add(group);
     return input;
 }
 static void Preview(Window owner,BitmapSource bitmap){
     var viewer=new Window{Owner=owner,Title="캡처 원본",WindowStartupLocation=WindowStartupLocation.CenterOwner};
     SizeToWorkArea(viewer,1440,990,580,380);
     var panel=new DockPanel();SetAppearance(viewer);viewer.Content=panel;var tools=Line();tools.Margin=new Thickness(10);DockTop(panel,tools);
     var image=new Image{Source=bitmap,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
     var scroll=new ScrollViewer{Content=image};panel.Children.Add(scroll);
     Action<bool> zoom=fit=>{
         scroll.HorizontalScrollBarVisibility=fit?ScrollBarVisibility.Disabled:ScrollBarVisibility.Auto;
         scroll.VerticalScrollBarVisibility=fit?ScrollBarVisibility.Disabled:ScrollBarVisibility.Auto;
         image.Width=fit?double.NaN:bitmap.PixelWidth;image.Height=fit?double.NaN:bitmap.PixelHeight;
     };
     var actual=Btn("원본 크기",()=>zoom(false));actual.Margin=new Thickness(0,0,8,0);
     tools.Children.Add(actual);tools.Children.Add(Btn("창에 맞춤",()=>zoom(true)));zoom(false);viewer.ShowDialog();
 }
 static void ConfigureImage(Window owner,RichTextBox body,Image image){
     var bitmap=image.Source as BitmapSource;if(bitmap==null)return;
     Action resize=()=>{if(body.ActualWidth>40)image.Width=Math.Min(bitmap.PixelWidth,body.ActualWidth-36);};
     image.Stretch=Stretch.Uniform;image.Height=double.NaN;resize();body.SizeChanged+=(o,e)=>resize();
     image.PreviewMouseLeftButtonDown+=(o,e)=>{if(e.ClickCount==2){e.Handled=true;Preview(owner,bitmap);}};
 }
 public static FlowDocument Document(Job j){var d=new FlowDocument();if(j.Body!=null)using(var m=new MemoryStream(j.Body))new TextRange(d.ContentStart,d.ContentEnd).Load(m,DataFormats.XamlPackage);return d;}
 void Edit(Job old){var j=old??new Job{Received=Day(DateTime.Today)};var w=new Window{Owner=this,Title=old==null?"새 업무":"업무 상세",WindowStartupLocation=WindowStartupLocation.CenterOwner};
 SizeToWorkArea(w,1440,990,650,490);
 SetAppearance(w);
 var page=new DockPanel { Background=Brushes.White };
 w.Content=page;
 var toolbar=new DockPanel { Margin=new Thickness(16,8,16,8) };
 DockTop(page,toolbar); DockTop(page,Rule());
 var footer=Line(); DockPanel.SetDock(footer,Dock.Right); toolbar.Children.Add(footer);
 var back=Quiet(j.Trash ? "‹  휴지통" : "‹  업무 목록",()=>w.Close());
 back.HorizontalAlignment=HorizontalAlignment.Left; toolbar.Children.Add(back);

 var article=new DockPanel { Margin=new Thickness(28,15,28,20) };
 page.Children.Add(article);
 var fields=new StackPanel(); DockTop(article,fields);
 var state=new ComboBox { Style=(Style)theme["StateSelector"], ItemsSource=States,
     SelectedItem=j.State, HorizontalAlignment=HorizontalAlignment.Left, Width=99, Height=28,
     Foreground=accent, FontWeight=FontWeights.SemiBold };
 System.Windows.Automation.AutomationProperties.SetName(state,"상태"); 
 var titleBar=new DockPanel(); fields.Children.Add(titleBar);
 DockPanel.SetDock(state,Dock.Right);titleBar.Children.Add(state);
 titleBar.Children.Add(Text("업무명",14,ink,true));
 var title=ArticleInput("업무명",j.Title,"업무명 입력");
 title.FontSize=28; title.FontWeight=FontWeights.Bold; title.Height=double.NaN; title.MinHeight=46;
 title.TextWrapping=TextWrapping.Wrap; title.MaxLines=3; title.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
 title.Margin=new Thickness(0,5,0,14);fields.Children.Add(title);
 var meta=new System.Windows.Controls.Primitives.UniformGrid { Columns=4, Margin=new Thickness(0,0,-12,5) };
 meta.SizeChanged+=(o,e)=>meta.Columns=meta.ActualWidth<710 ? 2 : 4;
 fields.Children.Add(meta);
 var who=Metadata(meta,"요청자 / 부서",j.Who,"요청자 또는 부서 입력");
 var received=Metadata(meta,"접수일",j.Received,"연-월-일");
 var due=Metadata(meta,"완료예정일",j.Due,"날짜 또는 미정");
 var memo=Metadata(meta,"비고",j.Memo,"비고 입력");
 var bodyLabel=Text("업무 내용",14,ink,true);
 bodyLabel.Margin=new Thickness(0,0,0,6);DockTop(article,bodyLabel);
 var body=new RichTextBox(Document(j)) { VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
     Padding=new Thickness(12), FontSize=15, Background=Brushes.White, Foreground=ink,
     BorderThickness=new Thickness(0), IsDocumentEnabled=true };
 body.Document.PagePadding=new Thickness(0);
 var bodyArea=new Grid();bodyArea.Children.Add(body);
 var bodyEdge=new Border { BorderBrush=Paint("#C6CEC9"), BorderThickness=new Thickness(1),
     CornerRadius=new CornerRadius(6),Padding=new Thickness(2),Child=bodyArea };
 article.Children.Add(bodyEdge);
 body.IsKeyboardFocusWithinChanged+=(o,e)=>bodyEdge.BorderBrush=body.IsKeyboardFocusWithin && !body.IsReadOnly ? accent : Paint("#C6CEC9");
 System.Windows.Automation.AutomationProperties.SetName(body,"업무 내용");
 if(old==null){
     var hint=Text("업무 내용을 쓰거나 캡처를 붙여넣으세요.",14,muted);
     hint.Margin=new Thickness(13,12,0,0); hint.VerticalAlignment=VerticalAlignment.Top;
     hint.IsHitTestVisible=false; bodyArea.Children.Add(hint);
     body.TextChanged+=(o,e)=>hint.Visibility=Visibility.Collapsed;
     body.GotKeyboardFocus+=(o,e)=>hint.Visibility=Visibility.Collapsed;
 }
 for(var pos=body.Document.ContentStart;pos!=null;pos=pos.GetNextContextPosition(LogicalDirection.Forward)){
     if(pos.GetPointerContext(LogicalDirection.Forward)!=TextPointerContext.EmbeddedElement)continue;
     var embedded=pos.GetAdjacentElement(LogicalDirection.Forward) as Image;if(embedded!=null)ConfigureImage(w,body,embedded);
 }
 if(j.Trash){
     foreach(var input in new[]{title,who,received,due,memo})input.IsReadOnly=true;
     state.IsEnabled=false;body.IsReadOnly=true;
     footer.Children.Add(Danger("영구 삭제",()=>{if(DeleteForever(j))w.Close();}));
     footer.Children.Add(FooterAction("복원",()=>{if(MoveTrash(j))w.Close();}));w.ShowDialog();return;
 }
 bool dirty=false,closed=false;foreach(var t in new[]{title,who,received,due,memo})t.TextChanged+=(o,e)=>dirty=true;state.SelectionChanged+=(o,e)=>dirty=true;body.TextChanged+=(o,e)=>dirty=true;
 DataObject.AddPastingHandler(body,(o,e)=>{if(e.DataObject.GetDataPresent(DataFormats.Bitmap)){var image=e.DataObject.GetData(DataFormats.Bitmap) as BitmapSource;if(image!=null){e.CancelCommand();body.Selection.Text="";var picture=new Image{Source=image};ConfigureImage(w,body,picture);var inline=new InlineUIContainer(picture,body.CaretPosition);body.CaretPosition=inline.ElementEnd;}}else if(e.DataObject.GetDataPresent(DataFormats.UnicodeText))e.FormatToApply=DataFormats.UnicodeText;});
 Func<bool> save=()=>{var date=Date(received.Text,DateTime.Today.Year);if(title.Text.Trim()==""||!date.HasValue){MessageBox.Show("업무명과 접수일을 확인하세요.");return false;}var k=j.Copy();k.Title=title.Text.Trim();k.Who=who.Text;k.Received=Day(date.Value);k.Due=DueValue(due.Text,date.Value.Year);k.Memo=memo.Text;SetStatus(k,(string)state.SelectedItem);using(var m=new MemoryStream()){new TextRange(body.Document.ContentStart,body.Document.ContentEnd).Save(m,DataFormats.XamlPackage);k.Body=m.ToArray();}Update(old,k);closed=true;return true;};
 Action finish=()=>{if(save())w.Close();};
 footer.Children.Add(Btn(j.State=="완료"?"다시 진행":"작업완료",()=>{
     state.SelectedItem=j.State=="완료"?"진행중":"완료";finish();
 }));
 footer.Children.Add(FooterAction("저장",finish));
 if(old!=null){
     var menu=new ContextMenu();
     menu.Items.Add(Option("휴지통으로 이동",()=>{if(MoveTrash(j)){closed=true;w.Close();}}));
     var more=Quiet("⋯",()=>menu.IsOpen=true);
     more.FontSize=18;more.ToolTip="더 보기";menu.PlacementTarget=more;
     menu.Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom;
     footer.Children.Add(more);
 }
 w.PreviewKeyDown+=(o,e)=>{if(e.Key==Key.S&&Keyboard.Modifiers==ModifierKeys.Control){e.Handled=true;try{finish();}catch(Exception ex){MessageBox.Show(ex.Message);}}};w.Closing+=(o,e)=>{if(closed||!dirty)return;var a=MessageBox.Show(w,"변경 사항을 저장하고 닫을까요?","업무 상세",MessageBoxButton.YesNoCancel);try{e.Cancel=a==MessageBoxResult.Cancel||(a==MessageBoxResult.Yes&&!save());}catch(Exception ex){MessageBox.Show(ex.Message);e.Cancel=true;}};if(old==null)w.ContentRendered+=(o,e)=>title.Focus();w.ShowDialog();}
 void Export(){var selected=Visible().Where(picks.Contains).ToList();if(selected.Count==0)return;var detail=MessageBox.Show("선택한 업무 "+selected.Count+"건에 본문과 캡처를 담은 상세 시트도 포함할까요?","엑셀 내보내기",MessageBoxButton.YesNoCancel);if(detail==MessageBoxResult.Cancel)return;var dlg=new SaveFileDialog{Filter="Excel (*.xlsx)|*.xlsx",FileName="업무일정_"+Day(DateTime.Today)};if(dlg.ShowDialog()!=true)return;ExcelReport.Write(dlg.FileName,selected,detail==MessageBoxResult.Yes);MessageBox.Show("엑셀 파일을 저장했습니다.");}
}
