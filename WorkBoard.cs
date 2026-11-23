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
 static Brush ink=new SolidColorBrush(Color.FromRgb(32,53,72)), teal=new SolidColorBrush(Color.FromRgb(12,112,103));
 static Button Btn(string s,Action a,bool primary=false){
 var b=new Button{Content=s,Padding=new Thickness(12,6,12,6),Margin=new Thickness(3),Background=primary?teal:Brushes.White,Foreground=primary?Brushes.White:ink,BorderBrush=Brushes.LightGray,Cursor=Cursors.Hand};
 b.Click+=(o,e)=>{try{a();}catch(Exception ex){MessageBox.Show(ex.GetBaseException().Message,"처리할 수 없습니다");}};return b;}
 static TextBlock Label(string s){return new TextBlock{Text=s,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(5),TextTrimming=TextTrimming.CharacterEllipsis,ToolTip=s};}
 static StackPanel Line(){return new StackPanel{Orientation=Orientation.Horizontal};}
 static void Top(DockPanel p,UIElement c){DockPanel.SetDock(c,Dock.Top);p.Children.Add(c);}
 static string Day(DateTime d){return d.ToString("yyyy-MM-dd");}
 static DateTime? Date(string s,int year){
 DateTime d; s=(s??"").Trim().Replace('/','-').Replace('.','-');
 if(s.Split('-').Length==2)s=year+"-"+s;
 return DateTime.TryParseExact(s,new[]{"yyyy-M-d","yyyy-MM-dd","yyyy-M-dd","yyyy-MM-d"},CultureInfo.InvariantCulture,DateTimeStyles.None,out d)?d:(DateTime?)null;}
 void Save(List<Job> next){
 string temp=file+".tmp"; using(var stream=File.Create(temp)){serializer.Serialize(stream,next);stream.Flush(true);}
 if(File.Exists(file))File.Replace(temp,file,file+".bak");else File.Move(temp,file); jobs=next;
 }
 void Update(Job old,Job j){var next=jobs.ToList();if(old==null)next.Insert(0,j);else next[next.IndexOf(old)]=j;Save(next);picks.Remove(old);Draw();}
 public Board(){
 Title="내 업무대장";Width=1080;Height=630;MinWidth=820;MinHeight=420;FontFamily=new FontFamily("Malgun Gothic");FontSize=14;Foreground=ink;
 if(File.Exists(file))using(var f=File.OpenRead(file))jobs=(List<Job>)serializer.Deserialize(f);
 var p=new DockPanel{Margin=new Thickness(20)};Content=p;
 var bar=new DockPanel();Top(p,bar);var actions=Line();DockPanel.SetDock(actions,Dock.Right);bar.Children.Add(actions);
 actions.Children.Add(Btn("+ 새 업무",()=>Edit(null),true));export=Btn("엑셀 내보내기",Export);actions.Children.Add(export);
 bar.Children.Add(new TextBlock{Text="내 업무대장",FontSize=28,FontWeight=FontWeights.Bold});Top(p,tabs);
 var filters=Line();Top(p,filters);filters.Children.Add(Label("접수일"));filters.Children.Add(start);filters.Children.Add(Label("~"));filters.Children.Add(end);
 filters.Children.Add(Btn("조회",Filter));filters.Children.Add(Btn("초기화",()=>{start.Clear();end.Clear();Filter();}));filters.Children.Add(Label("하루 조회는 첫 칸만 입력"));foreach(var box in new[]{start,end})box.KeyDown+=(o,e)=>{if(e.Key==Key.Enter)Filter();};
 var scroll=new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,12,0,0)};p.Children.Add(scroll);Draw();}
 void Filter(){var a=Date(start.Text,DateTime.Today.Year);var b=Date(end.Text,a.HasValue?a.Value.Year:DateTime.Today.Year);
 if((start.Text.Trim()!=""&&!a.HasValue)||(end.Text.Trim()!=""&&!b.HasValue)||a>b){MessageBox.Show("접수일을 11/23 또는 2026-11-23 형식으로 입력하고 기간을 확인하세요.");return;}from=a;to=b??a;picks.Clear();Draw();}
 static Grid Row(){var g=new Grid{MinHeight=38};foreach(var w in new[]{40,230,105,130,85,225})g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(w,GridUnitType.Star)});return g;}
 static void Cell(Grid g,int n,UIElement v){Grid.SetColumn(v,n);g.Children.Add(v);}
 List<Job> Visible(){var q=jobs.Where(j=>j.Trash==(mode=="휴지통")&&(mode!="미완료"||j.State!="완료")&&(mode!="완료"||j.State=="완료")&&(!from.HasValue||String.CompareOrdinal(j.Received,Day(from.Value))>=0)&&(!to.HasValue||String.CompareOrdinal(j.Received,Day(to.Value))<=0));
 return (byDue?q.OrderBy(j=>Date(j.Due,DateTime.Today.Year)??DateTime.MaxValue):q.OrderByDescending(j=>j.Received)).ToList();}
 void Draw(){tabs.Children.Clear();foreach(string m in new[]{"미완료","전체","완료","휴지통"})tabs.Children.Add(Btn(m,()=>{mode=m;picks.Clear();Draw();},mode==m));
 rows.Children.Clear();var list=Visible();picks.IntersectWith(list);export.IsEnabled=picks.Count>0&&mode!="휴지통";export.Content="엑셀 내보내기 ("+picks.Count+")";
 var h=Row();h.Background=Brushes.AliceBlue;rows.Children.Add(h);var all=new CheckBox{Margin=new Thickness(8),ToolTip="현재 목록 전체 선택",IsChecked=list.Count>0&&list.All(picks.Contains)};if(mode!="휴지통")Cell(h,0,all);all.Click+=(o,e)=>{picks.Clear();if(all.IsChecked==true)picks.UnionWith(list);Draw();};
 Cell(h,1,Label("업무명"));Cell(h,2,Label("접수일"));Cell(h,3,Btn("완료예정일 ↕",()=>{byDue=!byDue;Draw();}));Cell(h,4,Label("상태"));Cell(h,5,Label("진행 메모"));
 foreach(var j in list){var r=Row();rows.Children.Add(r);var c=new CheckBox{Margin=new Thickness(8),IsChecked=picks.Contains(j)};if(!j.Trash)Cell(r,0,c);c.Click+=(o,e)=>{if(c.IsChecked==true)picks.Add(j);else picks.Remove(j);Draw();};
 var title=Btn(j.Title,()=>{if(j.Trash){if(MessageBox.Show(j.Title+" 업무를 복원할까요?","복원",MessageBoxButton.YesNo)==MessageBoxResult.Yes){var k=j.Copy();k.Trash=false;Update(j,k);}}else Edit(j);});title.HorizontalContentAlignment=HorizontalAlignment.Left;Cell(r,1,title);
 Cell(r,2,Label(j.Received));var due=Label(j.Due==""?"미정":j.Due);if(j.State!="완료"&&Date(j.Due,DateTime.Today.Year)<DateTime.Today)due.Foreground=Brushes.Firebrick;Cell(r,3,due);Cell(r,4,Label(j.State));
 var memo=Label(j.Memo);Cell(r,5,memo);memo.MouseLeftButtonDown+=(o,e)=>{if(e.ClickCount!=2||j.Trash)return;var t=new TextBox{Text=j.Memo};r.Children.Remove(memo);Cell(r,5,t);t.Focus();t.SelectAll();bool done=false;Action commit=()=>{if(done)return;done=true;try{var k=j.Copy();k.Memo=t.Text;Update(j,k);}catch(Exception ex){MessageBox.Show(ex.Message);Draw();}};t.LostKeyboardFocus+=(s,a)=>commit();t.KeyDown+=(s,a)=>{if(a.Key==Key.Enter)commit();if(a.Key==Key.Escape){done=true;Draw();}};};}
 if(list.Count==0)rows.Children.Add(Label(mode=="휴지통"?"휴지통이 비어 있습니다.":"표시할 업무가 없습니다."));}
 static TextBox Field(StackPanel p,string caption,string value){var line=new DockPanel{Margin=new Thickness(2)};p.Children.Add(line);var l=Label(caption);l.Width=100;line.Children.Add(l);var t=new TextBox{Text=value,Padding=new Thickness(5)};line.Children.Add(t);return t;}
 public static FlowDocument Document(Job j){var d=new FlowDocument();if(j.Body!=null)using(var m=new MemoryStream(j.Body))new TextRange(d.ContentStart,d.ContentEnd).Load(m,DataFormats.XamlPackage);return d;}
 void Edit(Job old){var j=old??new Job{Received=Day(DateTime.Today)};var w=new Window{Owner=this,Title=old==null?"새 업무":"업무 상세",Width=720,Height=640,MinHeight=530,MinWidth=490,WindowStartupLocation=WindowStartupLocation.CenterOwner};
 var p=new DockPanel{Margin=new Thickness(16)};w.Content=p;var fields=new StackPanel();Top(p,fields);var title=Field(fields,"업무명",j.Title);var who=Field(fields,"요청자 / 부서",j.Who);var received=Field(fields,"접수일",j.Received);var due=Field(fields,"완료예정일",j.Due);var memo=Field(fields,"진행 메모",j.Memo);var line=Line();fields.Children.Add(line);line.Children.Add(Label("상태"));var state=new ComboBox{ItemsSource=new[]{"진행중","회신대기","완료"},SelectedItem=j.State,Width=120};line.Children.Add(state);fields.Children.Add(Label("업무 내용"));var footer=Line();DockPanel.SetDock(footer,Dock.Bottom);p.Children.Add(footer);
 var body=new RichTextBox(Document(j)){VerticalScrollBarVisibility=ScrollBarVisibility.Auto};p.Children.Add(body);bool dirty=false,closed=false;foreach(var t in new[]{title,who,received,due,memo})t.TextChanged+=(o,e)=>dirty=true;state.SelectionChanged+=(o,e)=>dirty=true;body.TextChanged+=(o,e)=>dirty=true;
 DataObject.AddPastingHandler(body,(o,e)=>{if(e.DataObject.GetDataPresent(DataFormats.Bitmap)){var image=e.DataObject.GetData(DataFormats.Bitmap) as BitmapSource;if(image!=null){e.CancelCommand();body.Selection.Text="";var inline=new InlineUIContainer(new Image{Source=image,Width=Math.Min(580,image.Width)},body.CaretPosition);body.CaretPosition=inline.ElementEnd;}}else if(e.DataObject.GetDataPresent(DataFormats.UnicodeText))e.FormatToApply=DataFormats.UnicodeText;});
 Func<bool> save=()=>{var date=Date(received.Text,DateTime.Today.Year);if(title.Text.Trim()==""||!date.HasValue){MessageBox.Show("업무명과 접수일을 확인하세요.");return false;}var k=j.Copy();k.Title=title.Text.Trim();k.Who=who.Text;k.Received=Day(date.Value);string raw=due.Text.Trim();var dd=Date(raw,date.Value.Year);k.Due=dd.HasValue?Day(dd.Value):raw;k.Memo=memo.Text;k.State=(string)state.SelectedItem;k.Finished=k.State=="완료"?(j.Finished==""?Day(DateTime.Today):j.Finished):"";using(var m=new MemoryStream()){new TextRange(body.Document.ContentStart,body.Document.ContentEnd).Save(m,DataFormats.XamlPackage);k.Body=m.ToArray();}Update(old,k);closed=true;return true;};
 Action finish=()=>{if(save())w.Close();};if(old!=null)footer.Children.Add(Btn("삭제",()=>{if(MessageBox.Show(j.Title+" 업무를 휴지통으로 옮길까요?","삭제",MessageBoxButton.YesNo)==MessageBoxResult.Yes){var k=j.Copy();k.Trash=true;Update(old,k);closed=true;w.Close();}}));footer.Children.Add(Btn(j.State=="완료"?"다시 진행":"작업완료",()=>{state.SelectedItem=j.State=="완료"?"진행중":"완료";finish();}));footer.Children.Add(Btn("저장",finish,true));w.PreviewKeyDown+=(o,e)=>{if(e.Key==Key.S&&Keyboard.Modifiers==ModifierKeys.Control){e.Handled=true;try{finish();}catch(Exception ex){MessageBox.Show(ex.Message);}}};w.Closing+=(o,e)=>{if(closed||!dirty)return;var a=MessageBox.Show(w,"변경 사항을 저장하고 닫을까요?","업무 상세",MessageBoxButton.YesNoCancel);try{e.Cancel=a==MessageBoxResult.Cancel||(a==MessageBoxResult.Yes&&!save());}catch(Exception ex){MessageBox.Show(ex.Message);e.Cancel=true;}};title.Focus();w.ShowDialog();}
 void Export(){var selected=Visible().Where(picks.Contains).ToList();if(selected.Count==0)return;var detail=MessageBox.Show("선택한 업무 "+selected.Count+"건에 본문과 캡처를 담은 상세 시트도 포함할까요?","엑셀 내보내기",MessageBoxButton.YesNoCancel);if(detail==MessageBoxResult.Cancel)return;var dlg=new SaveFileDialog{Filter="Excel (*.xlsx)|*.xlsx",FileName="업무일정_"+Day(DateTime.Today)};if(dlg.ShowDialog()!=true)return;ExcelReport.Write(dlg.FileName,selected,detail==MessageBoxResult.Yes);MessageBox.Show("엑셀 파일을 저장했습니다.");}
 [STAThread] public static void Main(){bool first;using(var gate=new System.Threading.Mutex(true,"Local\\PersonalWorkBoard_4827",out first)){if(!first){MessageBox.Show("업무대장이 이미 열려 있습니다.");return;}try{new Application().Run(new Board());}catch(Exception ex){MessageBox.Show(ex.Message,"업무대장");}}}
}
