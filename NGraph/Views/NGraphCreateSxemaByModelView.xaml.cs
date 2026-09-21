
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using NGraph.ViewModels;
using NGraph.Core;
using Nice3point.Revit.Extensions.Runtime;

namespace NGraph.Views;

public sealed partial class NGraphCreateSxemaByModelView
{
        public Object Cancel { get; set; }
        IList<Element> Elements { get; set; } 
        public int Selector { get; set; } = 0;
        public NGraphCreateSxemaByModelView(NGraphCreateSxemaByModelViewModel viewModel)
        {
            DataContext = viewModel;
            Elements = new FilteredElementCollector(viewModel.Doc).OfCategory(BuiltInCategory.OST_ElectricalEquipment).WhereElementIsNotElementType().ToElements();
            Helpers helpers = new Helpers();
            InitializeComponent();
            this.Closing += Window_Closing;
            var GenericAnnotation = helpers.AllElementsOfCategory(viewModel.Doc, BuiltInCategory.OST_GenericAnnotation).ToList(); //Типовые аннотации
            BySpace.IsChecked = true;
            //parama = viewModel.stringparam;



            List<Parameter> parameters = new List<Parameter>();
            List<Parameter> parametersOfType = new List<Parameter>();

            foreach (var e in Elements)
            {
                GetParemeterList_Instance_And_Type(e.ParametersMap, ref parameters);
                GetParemeterList_Instance_And_Type((e as FamilyInstance).Symbol.ParametersMap, ref parametersOfType);


            }

            CB_Param.ItemsSource = parameters.OrderBy(i => i.Definition.Name);
            CB_Param.DisplayMemberPath = "Definition.Name";
            CB_Param.SelectedItem = parameters.Where(i=>i.Definition.Name == "Комментарии").FirstOrDefault();

            



        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Cancel = true;
            e.Cancel = false;
        }


        private void Confirm_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Close();
            Cancel = false;
        }

        private void Value_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            
        }

        private void Param_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            
        }

        private void BySpace_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (BySpace.IsChecked==true)
            {
                Selector = 0;
                Description.Content = "Построение структурной схемы по пространствам";
            }


        }
              

        private void ByParameter_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ByParameter.IsChecked == true)
            {
                Selector = 1;
                Description.Content = "Построение структурной схемы по параметрам";
            }


        }
        /// <summary>
        /// Список всех возможных значений для элементов у определенного параметра
        /// </summary>
        /// <param name="elements"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        private List<string> GetSrtingLists_forCheckBox(IList<Element> elements, Parameter parameter)
        {
            var def = parameter.Definition;
            List<string> list = [];
            foreach (var element in elements)
            {
                //var elementOFType = (element as FamilyInstance).Symbol;
                string item_s;
                try
                {
#if REVIT2022_OR_GREATER
                item_s = element.FindParameter(def.Name).AsValueString();
#else
                    item_s = element.LookupParameter(def.Name).AsString();
#endif
                    
                }
                catch
                {
                    item_s = "(null) for any Instaces";
                }

                if (item_s.IsNullOrEmpty() & list.Contains(item_s) == false)
                { list.Add(item_s); }
                else if (list.Contains(item_s))
                { continue; }
                else
                { list.Add(item_s); }
            }
            list.RemoveAll(i => i.IsNullOrEmpty());
            list.Sort();
            if (list.Count == 0) { list.Add("(Empty)"); }
            return list;
        }

        private void GetParemeterList_Instance_And_Type(ParameterMap parameterMap, ref List<Parameter> parameters)
        {

            foreach (var p in parameterMap)
            {
                Parameter parameter = p as Parameter;

#if REVIT2022_OR_GREATER
                var storageType = parameter.StorageType;
#else
                    var storageType = parameter.StorageType;
#endif

                if (storageType == StorageType.String || storageType == StorageType.Integer || storageType == StorageType.Double)
                {
                    if (parameters.Any(i => i.Definition.Name == (p as Parameter).Definition.Name)) //Если уже есть в списке
                    {
                        continue;
                    }
                    else
                    {
                        parameters.Add(p as Parameter);
                    }
                }
                else { continue; }

            }


        }

        private void CB_Param_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var parameter = CB_Param.SelectedItem as Parameter;
            CB_Value.ItemsSource = GetSrtingLists_forCheckBox(Elements, parameter);
            CB_Value.SelectedIndex = 0;
        }

        private void CB_Value_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
  
}