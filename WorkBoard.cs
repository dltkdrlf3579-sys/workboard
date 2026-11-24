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
public class Board : Window {
 static readonly string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"업무대장.xml");
 static XmlSerializer serializer=new XmlSerializer(typeof(List<Job>));
 List<Job> jobs=new List<Job>();
 StackPanel rows=new StackPanel(), tabs=new StackPanel{Orientation=Orientation.Horizontal};
 TextBox start=new TextBox{Width=105}, end=new TextBox{Width=105};
 string mode="미완료"; DateTime? from,to; bool byDue;
 HashSet<Job> picks=new HashSet<Job>(); Button export;
 static Brush ink=new SolidColorBrush(Color.FromRgb(32,53,72)), teal=new SolidColorBrush(Color.FromRgb(68,69,161));
 static readonly ControlTemplate RoundedButton=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(
 @"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>
 <Border Name='edge' CornerRadius='6' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'>
 <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border>
 <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='edge' Property='Opacity' Value='0.78'/></Trigger>
 <Trigger Property='IsEnabled' Value='False'><Setter TargetName='edge' Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
 static Button Quiet(string caption, Action action){
     var button=Btn(caption,action);
     button.Background=Brushes.Transparent;
     button.BorderThickness=new Thickness(0);
     return button;
 }
 static Button Btn(string s,Action a,bool primary=false){
 var b=new Button{Content=s,Padding=new Thickness(12,6,12,6),Margin=new Thickness(3),Background=primary?teal:Brushes.White,Foreground=primary?Brushes.White:ink,BorderBrush=Brushes.LightGray,Cursor=Cursors.Hand};
 b.Height=34;b.Margin=new Thickness(4,2,4,2);b.Padding=new Thickness(14,0,14,0);
 b.Template=RoundedButton;
 b.Click+=(o,e)=>{try{a();}catch(Exception ex){MessageBox.Show(ex.GetBaseException().Message,"처리할 수 없습니다");}};return b;}
 static TextBlock Label(string s){return new TextBlock{Text=s,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(5),TextTrimming=TextTrimming.CharacterEllipsis,ToolTip=s};}
 static StackPanel Line(){return new StackPanel{Orientation=Orientation.Horizontal};}
 static void Top(DockPanel p,UIElement c){DockPanel.SetDock(c,Dock.Top);p.Children.Add(c);}
 static string Day(DateTime d){return d.ToString("yyyy-MM-dd");}
 static DateTime? Date(string s,int year){
 DateTime d; s=(s??"").Trim().Replace('/','-').Replace('.','-');
 if(s.Split('-').Length==2)s=year+"-"+s;
 return DateTime.TryParseExact(s,new[]{"yyyy-M-d","yyyy-MM-dd","yyyy-M-dd","yyyy-MM-d"},CultureInfo.InvariantCulture,DateTimeStyles.None,out d)?d:(DateTime?)null;}
 void Save(List<Job> next, bool purge=false){
 string temp=file+".tmp"; using(var stream=File.Create(temp)){serializer.Serialize(stream,next);stream.Flush(true);}
 if(purge)File.Delete(file+".bak");
 if(File.Exists(file))File.Replace(temp,file,purge?null:file+".bak");else File.Move(temp,file); jobs=next;
 }
 void Update(Job old,Job j){var next=jobs.ToList();if(old==null)next.Insert(0,j);else next[next.IndexOf(old)]=j;Save(next);if(old!=null && picks.Remove(old) && !j.Trash)picks.Add(j);Draw();}
 public Board(){
 Title="업무 목록";Width=1080;Height=630;MinWidth=820;MinHeight=420;FontFamily=new FontFamily("Malgun Gothic");FontSize=14;Foreground=ink;
 if(File.Exists(file))using(var f=File.OpenRead(file))jobs=(List<Job>)serializer.Deserialize(f);
 var p=new DockPanel{Margin=new Thickness(20)};Content=p;
 var bar=new DockPanel();Top(p,bar);var actions=Line();DockPanel.SetDock(actions,Dock.Right);bar.Children.Add(actions);
 export=Btn("엑셀 내보내기",Export);actions.Children.Add(export);actions.Children.Add(Btn("+ 새 업무",()=>Edit(null),true));
 bar.Children.Add(new TextBlock{Text="업무 목록",FontSize=28,FontWeight=FontWeights.Bold});var navigation=new DockPanel{Margin=new Thickness(0,12,0,8)};Top(p,navigation);var trash=Quiet("휴지통",()=>{mode="휴지통";picks.Clear();Draw();});DockPanel.SetDock(trash,Dock.Right);navigation.Children.Add(trash);navigation.Children.Add(tabs);
 var filters=Line();Top(p,filters);filters.Children.Add(Label("접수일"));filters.Children.Add(start);filters.Children.Add(Label("~"));filters.Children.Add(end);
 filters.Children.Add(Btn("조회",Filter));filters.Children.Add(Quiet("초기화",()=>{start.Clear();end.Clear();Filter();}));filters.Children.Add(Label("하루 조회는 첫 칸만 입력"));foreach(var box in new[]{start,end})box.KeyDown+=(o,e)=>{if(e.Key==Key.Enter)Filter();};
 var scroll=new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,12,0,0)};p.Children.Add(scroll);Draw();}
 void Filter(){var a=Date(start.Text,DateTime.Today.Year);var b=Date(end.Text,a.HasValue?a.Value.Year:DateTime.Today.Year);
 if((start.Text.Trim()!=""&&!a.HasValue)||(end.Text.Trim()!=""&&!b.HasValue)||a>b){MessageBox.Show("접수일을 11/23 또는 2026-11-23 형식으로 입력하고 기간을 확인하세요.");return;}from=a;to=b??a;picks.Clear();Draw();}
 static Grid Row(){var g=new Grid{MinHeight=44,Background=Brushes.Transparent};foreach(var w in new[]{36,1,120,136,84,164})g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(w,w==1?GridUnitType.Star:GridUnitType.Pixel)});return g;}
 static void Cell(Grid g,int n,UIElement v){Grid.SetColumn(v,n);g.Children.Add(v);}
 List<Job> Visible(){var q=jobs.Where(j=>j.Trash==(mode=="휴지통")&&(mode!="미완료"||j.State!="완료")&&(mode!="완료"||j.State=="완료")&&(!from.HasValue||String.CompareOrdinal(j.Received,Day(from.Value))>=0)&&(!to.HasValue||String.CompareOrdinal(j.Received,Day(to.Value))<=0));
 return (byDue?q.OrderBy(j=>Date(j.Due,DateTime.Today.Year)??DateTime.MaxValue):q.OrderByDescending(j=>j.Received)).ToList();}
 void Draw(){tabs.Children.Clear();foreach(string m in new[]{"미완료","전체","완료"}){
     int count=jobs.Count(j=>!j.Trash && (m=="전체" || (j.State=="완료")==(m=="완료")));
     var item=Quiet(m+"  "+count,()=>{mode=m;picks.Clear();Draw();});
     tabs.Children.Add(new Border{Child=item,BorderBrush=teal,BorderThickness=new Thickness(0,0,0,mode==m?2:0)});
 }
 rows.Children.Clear();var list=Visible();picks.IntersectWith(list);export.IsEnabled=picks.Count>0&&mode!="휴지통";export.Content="엑셀 내보내기 ("+picks.Count+")";
 var h=Row();h.Background=Brushes.AliceBlue;rows.Children.Add(h);var all=new CheckBox{Margin=new Thickness(8),ToolTip="현재 목록 전체 선택",IsChecked=list.Count>0&&list.All(picks.Contains)};if(mode!="휴지통")Cell(h,0,all);all.Click+=(o,e)=>{picks.Clear();if(all.IsChecked==true)picks.UnionWith(list);Draw();};
 Cell(h,1,Label("업무명"));Cell(h,2,Label("접수일"));Cell(h,3,Quiet("완료예정일 ↕",()=>{byDue=!byDue;Draw();}));Cell(h,4,Label("상태"));Cell(h,5,Label("비고"));
 foreach(var j in list){var r=Row();rows.Children.Add(r);var c=new CheckBox{Margin=new Thickness(8),IsChecked=picks.Contains(j)};if(!j.Trash)Cell(r,0,c);c.Click+=(o,e)=>{if(c.IsChecked==true)picks.Add(j);else picks.Remove(j);Draw();};
 var title=Quiet("",()=>Edit(j));
 var name=Label(j.Title);name.FontWeight=FontWeights.SemiBold;
 title.Content=name;title.HorizontalContentAlignment=HorizontalAlignment.Stretch;title.Padding=new Thickness(0);
 Cell(r,1,title);
 var menu=RowMenu(j);r.ContextMenu=menu;
 Action highlight=()=>r.Background=(picks.Contains(j)||r.IsMouseOver||menu.IsOpen)?Brushes.AliceBlue:Brushes.White;
 highlight();r.MouseEnter+=(o,e)=>highlight();r.MouseLeave+=(o,e)=>highlight();menu.Opened+=(o,e)=>highlight();menu.Closed+=(o,e)=>highlight();
 Cell(r,2,Label(j.Received));var due=Label(j.Due==""?"미정":j.Due);if(j.State!="완료"&&Date(j.Due,DateTime.Today.Year)<DateTime.Today)due.Foreground=Brushes.Firebrick;Cell(r,3,due);var badge=new Border{CornerRadius=new CornerRadius(5),Background=j.State=="회신대기"?Brushes.LemonChiffon:j.State=="완료"?Brushes.Honeydew:Brushes.Lavender,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Child=Label(j.State)};Cell(r,4,badge);
 var memo=Label(j.Memo);Cell(r,5,memo);memo.MouseLeftButtonDown+=(o,e)=>{if(e.ClickCount!=2||j.Trash)return;var t=new TextBox{Text=j.Memo};r.Children.Remove(memo);Cell(r,5,t);t.Focus();t.SelectAll();bool done=false;Action commit=()=>{if(done)return;done=true;try{var k=j.Copy();k.Memo=t.Text;Update(j,k);}catch(Exception ex){MessageBox.Show(ex.Message);Draw();}};t.LostKeyboardFocus+=(s,a)=>commit();t.KeyDown+=(s,a)=>{if(a.Key==Key.Enter)commit();if(a.Key==Key.Escape){done=true;Draw();}};};}
 if(list.Count==0)rows.Children.Add(Label(mode=="휴지통"?"휴지통이 비어 있습니다.":"표시할 업무가 없습니다."));}
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
         foreach(string state in new[]{"진행중","회신대기","완료"}){
             var item=Option(state,()=>{var next=j.Copy();next.State=state;next.Finished=state=="완료"?Day(DateTime.Today):"";Update(j,next);});
             item.IsEnabled=state!=j.State;states.Items.Add(item);
         }
         menu.Items.Add(states);menu.Items.Add(Option("업무명 복사",()=>Clipboard.SetText(j.Title)));
         menu.Items.Add(new Separator());menu.Items.Add(Option("휴지통으로 이동",()=>MoveTrash(j)));
     }
     return menu;
 }
 static TextBox Field(StackPanel p,string caption,string value){var line=new DockPanel{Margin=new Thickness(2)};p.Children.Add(line);var l=Label(caption);l.Width=100;line.Children.Add(l);var t=new TextBox{Text=value,Padding=new Thickness(5)};line.Children.Add(t);return t;}
 static TextBox Compact(Panel panel,string label,string value){
     var stack=new StackPanel{Margin=new Thickness(4)};panel.Children.Add(stack);stack.Children.Add(Label(label));
     var input=new TextBox{Text=value,Padding=new Thickness(6,4,6,4)};stack.Children.Add(input);return input;
 }
 static void Preview(Window owner,BitmapSource bitmap){
     var viewer=new Window{Owner=owner,Title="캡처 원본",Width=880,Height=620,WindowStartupLocation=WindowStartupLocation.CenterOwner};
     var panel=new DockPanel();viewer.Content=panel;var tools=Line();Top(panel,tools);
     var image=new Image{Source=bitmap,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
     var scroll=new ScrollViewer{Content=image};panel.Children.Add(scroll);
     Action<bool> zoom=fit=>{
         scroll.HorizontalScrollBarVisibility=fit?ScrollBarVisibility.Disabled:ScrollBarVisibility.Auto;
         scroll.VerticalScrollBarVisibility=fit?ScrollBarVisibility.Disabled:ScrollBarVisibility.Auto;
         image.Width=fit?double.NaN:bitmap.PixelWidth;image.Height=fit?double.NaN:bitmap.PixelHeight;
     };
     tools.Children.Add(Btn("원본 크기",()=>zoom(false)));tools.Children.Add(Btn("창에 맞춤",()=>zoom(true)));zoom(false);viewer.ShowDialog();
 }
 static void ConfigureImage(Window owner,RichTextBox body,Image image){
     var bitmap=image.Source as BitmapSource;if(bitmap==null)return;
     Action resize=()=>{if(body.ActualWidth>40)image.Width=Math.Min(bitmap.PixelWidth,body.ActualWidth-36);};
     image.Stretch=Stretch.Uniform;image.Height=double.NaN;resize();body.SizeChanged+=(o,e)=>resize();
     image.PreviewMouseLeftButtonDown+=(o,e)=>{if(e.ClickCount==2){e.Handled=true;Preview(owner,bitmap);}};
 }
 public static FlowDocument Document(Job j){var d=new FlowDocument();if(j.Body!=null)using(var m=new MemoryStream(j.Body))new TextRange(d.ContentStart,d.ContentEnd).Load(m,DataFormats.XamlPackage);return d;}
 void Edit(Job old){var j=old??new Job{Received=Day(DateTime.Today)};var w=new Window{Owner=this,Title=old==null?"새 업무":"업무 상세",Width=900,Height=700,MinHeight=530,MinWidth=650,WindowStartupLocation=WindowStartupLocation.CenterOwner};
 var p=new DockPanel{Margin=new Thickness(16)};w.Content=p;var fields=new StackPanel();Top(p,fields);var title=Field(fields,"업무명",j.Title);var dates=new System.Windows.Controls.Primitives.UniformGrid{Rows=1,Columns=3};fields.Children.Add(dates);
 var received=Compact(dates,"접수일",j.Received);var due=Compact(dates,"완료예정일",j.Due);
 var statusBox=new StackPanel{Margin=new Thickness(4)};dates.Children.Add(statusBox);statusBox.Children.Add(Label("상태"));
 var state=new ComboBox{ItemsSource=new[]{"진행중","회신대기","완료"},SelectedItem=j.State,Height=30};statusBox.Children.Add(state);
 var meta=new System.Windows.Controls.Primitives.UniformGrid{Rows=1,Columns=2};fields.Children.Add(meta);
 var who=Compact(meta,"요청자 / 부서",j.Who);var memo=Compact(meta,"비고",j.Memo);fields.Children.Add(Label("업무 내용"));var foot=new DockPanel{Margin=new Thickness(0,10,0,0)};DockPanel.SetDock(foot,Dock.Bottom);p.Children.Add(foot);var footer=Line();footer.HorizontalAlignment=HorizontalAlignment.Right;var danger=Line();foot.Children.Add(danger);foot.Children.Add(footer);
 var body=new RichTextBox(Document(j)){VerticalScrollBarVisibility=ScrollBarVisibility.Auto};p.Children.Add(body);body.IsDocumentEnabled=true;
 for(var pos=body.Document.ContentStart;pos!=null;pos=pos.GetNextContextPosition(LogicalDirection.Forward)){
     if(pos.GetPointerContext(LogicalDirection.Forward)!=TextPointerContext.EmbeddedElement)continue;
     var embedded=pos.GetAdjacentElement(LogicalDirection.Forward) as Image;if(embedded!=null)ConfigureImage(w,body,embedded);
 }
 if(j.Trash){
     foreach(var input in new[]{title,who,received,due,memo})input.IsReadOnly=true;
     state.IsEnabled=false;body.IsReadOnly=true;
     danger.Children.Add(Quiet("영구 삭제",()=>{if(DeleteForever(j))w.Close();}));footer.Children.Add(Btn("복원",()=>{if(MoveTrash(j))w.Close();},true));
     footer.Children.Add(Quiet("닫기",()=>w.Close()));w.ShowDialog();return;
 }
 bool dirty=false,closed=false;foreach(var t in new[]{title,who,received,due,memo})t.TextChanged+=(o,e)=>dirty=true;state.SelectionChanged+=(o,e)=>dirty=true;body.TextChanged+=(o,e)=>dirty=true;
 DataObject.AddPastingHandler(body,(o,e)=>{if(e.DataObject.GetDataPresent(DataFormats.Bitmap)){var image=e.DataObject.GetData(DataFormats.Bitmap) as BitmapSource;if(image!=null){e.CancelCommand();body.Selection.Text="";var picture=new Image{Source=image};ConfigureImage(w,body,picture);var inline=new InlineUIContainer(picture,body.CaretPosition);body.CaretPosition=inline.ElementEnd;}}else if(e.DataObject.GetDataPresent(DataFormats.UnicodeText))e.FormatToApply=DataFormats.UnicodeText;});
 Func<bool> save=()=>{var date=Date(received.Text,DateTime.Today.Year);if(title.Text.Trim()==""||!date.HasValue){MessageBox.Show("업무명과 접수일을 확인하세요.");return false;}var k=j.Copy();k.Title=title.Text.Trim();k.Who=who.Text;k.Received=Day(date.Value);string raw=due.Text.Trim();var dd=Date(raw,date.Value.Year);k.Due=dd.HasValue?Day(dd.Value):raw;k.Memo=memo.Text;k.State=(string)state.SelectedItem;k.Finished=k.State=="완료"?(j.Finished==""?Day(DateTime.Today):j.Finished):"";using(var m=new MemoryStream()){new TextRange(body.Document.ContentStart,body.Document.ContentEnd).Save(m,DataFormats.XamlPackage);k.Body=m.ToArray();}Update(old,k);closed=true;return true;};
 Action finish=()=>{if(save())w.Close();};if(old!=null)danger.Children.Add(Quiet("삭제",()=>{if(MoveTrash(j)){closed=true;w.Close();}}));footer.Children.Add(Btn(j.State=="완료"?"다시 진행":"작업완료",()=>{state.SelectedItem=j.State=="완료"?"진행중":"완료";finish();}));footer.Children.Add(Btn("저장",finish,true));w.PreviewKeyDown+=(o,e)=>{if(e.Key==Key.S&&Keyboard.Modifiers==ModifierKeys.Control){e.Handled=true;try{finish();}catch(Exception ex){MessageBox.Show(ex.Message);}}};w.Closing+=(o,e)=>{if(closed||!dirty)return;var a=MessageBox.Show(w,"변경 사항을 저장하고 닫을까요?","업무 상세",MessageBoxButton.YesNoCancel);try{e.Cancel=a==MessageBoxResult.Cancel||(a==MessageBoxResult.Yes&&!save());}catch(Exception ex){MessageBox.Show(ex.Message);e.Cancel=true;}};title.Focus();w.ShowDialog();}
 void Export(){var selected=Visible().Where(picks.Contains).ToList();if(selected.Count==0)return;var detail=MessageBox.Show("선택한 업무 "+selected.Count+"건에 본문과 캡처를 담은 상세 시트도 포함할까요?","엑셀 내보내기",MessageBoxButton.YesNoCancel);if(detail==MessageBoxResult.Cancel)return;var dlg=new SaveFileDialog{Filter="Excel (*.xlsx)|*.xlsx",FileName="업무일정_"+Day(DateTime.Today)};if(dlg.ShowDialog()!=true)return;ExcelReport.Write(dlg.FileName,selected,detail==MessageBoxResult.Yes);MessageBox.Show("엑셀 파일을 저장했습니다.");}
 [STAThread] public static void Main(){bool first;using(var gate=new System.Threading.Mutex(true,"Local\\PersonalWorkBoard_4827",out first)){if(!first){MessageBox.Show("업무대장이 이미 열려 있습니다.");return;}try{new Application().Run(new Board());}catch(Exception ex){MessageBox.Show(ex.Message,"업무대장");}}}
}
