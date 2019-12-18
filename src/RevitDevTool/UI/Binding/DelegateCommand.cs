/************************************************************************  
 * ClassName   :  DelegateCommand     
 * Description :       
 * Author      :  Einsam    
 * CreateTime  :  2019/8/27 14:56:07  
************************************************************************
 *  ▄         ▄  ▄▄▄▄▄▄▄▄▄▄  ▄▄▄▄▄▄▄▄▄▄▄  ▄▄       ▄▄ 
 * ▐░▌       ▐░▌▐░░░░░░░░░░▌▐░░░░░░░░░░░▌▐░░▌     ▐░░▌
 * ▐░▌       ▐░▌▐░█▀▀▀▀▀▀▀█░▌▀▀▀▀█░█▀▀▀▀ ▐░▌░▌   ▐░▐░▌
 * ▐░▌       ▐░▌▐░▌       ▐░▌    ▐░▌     ▐░▌▐░▌ ▐░▌▐░▌
 * ▐░▌       ▐░▌▐░█▄▄▄▄▄▄▄█░▌    ▐░▌     ▐░▌ ▐░▐░▌ ▐░▌
 * ▐░▌       ▐░▌▐░░░░░░░░░░▌     ▐░▌     ▐░▌  ▐░▌  ▐░▌
 * ▐░▌       ▐░▌▐░█▀▀▀▀▀▀▀█░▌    ▐░▌     ▐░▌   ▀   ▐░▌
 * ▐░▌       ▐░▌▐░▌       ▐░▌    ▐░▌     ▐░▌       ▐░▌
 * ▐░█▄▄▄▄▄▄▄█░▌▐░█▄▄▄▄▄▄▄█░▌▄▄▄▄█░█▄▄▄▄ ▐░▌       ▐░▌
 * ▐░░░░░░░░░░░▌▐░░░░░░░░░░▌▐░░░░░░░░░░░▌▐░▌       ▐░▌
 *  ▀▀▀▀▀▀▀▀▀▀▀  ▀▀▀▀▀▀▀▀▀▀  ▀▀▀▀▀▀▀▀▀▀▀  ▀         ▀ 
************************************************************************
 * Copyright @ u-BIM Dev. 2018 . All rights reserved.  
************************************************************************/

using System;
using System.Windows.Input;

namespace RevitDevTool.UI.Binding
{
    public class DelegateCommand : ICommand
    {
        /// <summary>
        /// 命令所需执行的事件
        /// </summary>
        public Action<object> ExecuteCommand { get; set; }

        /// <summary>
        /// 命令是否可用所执行的事件
        /// </summary>
        public Func<object, bool> CanExecuteCommand { get; set; }

        public DelegateCommand()
        {
        }

        public DelegateCommand(Action<object> execute, Func<object, bool> canExecute)
        {
            ExecuteCommand = execute;
            CanExecuteCommand = canExecute;
        }

        /// <summary>
        /// 命令可用性获取
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public bool CanExecute(object parameter)
        {
            if (CanExecuteCommand != null)
            {
                return CanExecuteCommand(parameter);
            }
            else
            {
                return true;
            }
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>
        /// 命令具体执行
        /// </summary>
        /// <param name="parameter"></param>
        public void Execute(object parameter)
        {
            ExecuteCommand(parameter);
        }
    }
}
