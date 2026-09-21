
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using NGraph.ViewModels;
using NGraph.Core;

namespace NGraph.Views;

public sealed partial class NGraphCreateFootorFromBdView
{
    public Object selectedSections {  get; set; }
    

    public Object Cancel { get; set; }

    public Object CreateFSA { get; set; }
        public NGraphCreateFootorFromBdView(NGraphCreateFootorFromBdViewModel viewModel)
        {
            DataContext = viewModel;
            Helpers helpers = new Helpers();
            InitializeComponent();
//var GenericAnnotation = helpers.AllElementsOfCategory(viewModel.Doc, BuiltInCategory.OST_GenericAnnotation).ToList(); //Типовые аннотации
//RB_Вытяжка.IsChecked = true;
            this.Closing += Window_Closing;
//CB_individual.ItemsSource = viewModel.BD.Секции.Where(x => x.Type == "BD_Готовое решение").GroupBy(x => x.Name).Select(x => x.Key).ToList();

            /*
            CB_individual.ItemsSource = viewModel.BD.Секции.Where(x => x.Type == "BD_Готовое решение");
            CB_individual.DisplayMemberPath = "Name";
            CB_individual.SelectedItem = viewModel.BD.Секции.FirstOrDefault();
            */
            selectedSectionsEmpty.ItemsSource = viewModel.Sections;
            selectedSectionsEmpty.DisplayMemberPath = "Name";
            selectedSectionsEmpty.SelectedItem = viewModel.Sections.FirstOrDefault();



        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Cancel = true;
            e.Cancel =false;
            /*
            // Диалог подтверждения закрытия
            MessageBoxResult result = MessageBox.Show(
                "Вы уверены, что хотите выйти?",
                "Закрытие окна",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            // Если пользователь нажал "Нет", отмена закрытия
            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;

            }
            */
        }
/*
        private void CB_individual_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            selectedSections =CB_individual.SelectedItem;
            
        }
  */      
        private void CB_individual_SelectionChangedEmpty(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            selectedSections =selectedSectionsEmpty.SelectedItem;
            
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
            Cancel = false;
        }
        private void MouseEnter(object sender, RoutedEventArgs e)
        {
            ИмяУстановки.Text = "";
           

        }

        private void RB_individual_Checked(object sender, RoutedEventArgs e)
        {

        }
/*
        private void RB_in_Checked(object sender, RoutedEventArgs e) //Приточка
        {
            Заслонка1.IsEnabled = false;
            Заслонка1селект.IsEnabled = false;
            Заслонка2.IsEnabled = true;
            Заслонка2селект.IsEnabled = true;
        }

        private void RB_in_out_Checked(object sender, RoutedEventArgs e) //ПВ
        {
            Заслонка1.IsEnabled = true;
            Заслонка1селект.IsEnabled = true;
            Заслонка2.IsEnabled = true;
            Заслонка2селект.IsEnabled = true;

        }

        private void RB_out_Checked(object sender, RoutedEventArgs e) //Вытяжка
        {
            Заслонка1.IsEnabled = true;
            Заслонка1селект.IsEnabled = true;
            Заслонка2.IsEnabled = false;
            Заслонка2селект.IsEnabled = false;

        }
*/
        private void IsFSAcreate_Checked(object sender, RoutedEventArgs e)
        {
            CreateFSA = true;
        }

        private void IsFSAcreate_Unchecked(object sender, RoutedEventArgs e)
        {
            CreateFSA = false;
        }
  
}