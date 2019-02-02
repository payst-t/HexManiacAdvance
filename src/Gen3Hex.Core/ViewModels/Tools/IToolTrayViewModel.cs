﻿using HavenSoft.Gen3Hex.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.Core.ViewModels.Tools {
   public interface IToolTrayViewModel : IReadOnlyList<IToolViewModel>, INotifyPropertyChanged {
      int SelectedIndex { get; set; }

      PCSTool StringTool { get; }

      IDisposable DeferUpdates { get; }

      void Schedule(Action action);
   }

   public class ToolTray : ViewModelCore, IToolTrayViewModel {
      private readonly IList<IToolViewModel> tools;
      private readonly StubCommand hideCommand;
      private readonly StubCommand stringToolCommand, tool2Command, tool3Command;
      private readonly HashSet<Action> deferredWork = new HashSet<Action>();

      private int selectedIndex;
      public int SelectedIndex {
         get => selectedIndex;
         set {
            if (TryUpdate(ref selectedIndex, value)) {
               hideCommand.CanExecuteChanged.Invoke(hideCommand, EventArgs.Empty);
            }
         }
      }

      public int Count => tools.Count;
      public IToolViewModel this[int index] => tools[index];

      public ICommand HideCommand => hideCommand;
      public ICommand StringToolCommand => stringToolCommand;
      public ICommand Tool2Command => tool2Command;
      public ICommand Tool3Command => tool3Command;

      public PCSTool StringTool => (PCSTool)tools[0];

      public IToolViewModel Tool2 => tools[1];

      public IToolViewModel Tool3 => tools[2];

      private StubDisposable currentDeferralToken;
      public IDisposable DeferUpdates {
         get {
            Debug.Assert(currentDeferralToken == null);
            currentDeferralToken = new StubDisposable {
               Dispose = () => {
                  foreach (var action in deferredWork) action();
                  deferredWork.Clear();
                  currentDeferralToken = null;
               }
            };
            return currentDeferralToken;
         }
      }

      public ToolTray(IDataModel model, Selection selection, ChangeHistory<ModelDelta> history) {
         tools = new IToolViewModel[] {
            new PCSTool(model, selection, history, this),
            new FillerTool("Tool2"),
            new FillerTool("Tool3"),
         };

         stringToolCommand = new StubCommand {
            CanExecute = ICommandExtensions.CanAlwaysExecute,
            Execute = arg => SelectedIndex = selectedIndex == 0 ? -1 : 0,
         };

         tool2Command = new StubCommand {
            CanExecute = ICommandExtensions.CanAlwaysExecute,
            Execute = arg => SelectedIndex = selectedIndex == 1 ? -1 : 1,
         };

         tool3Command = new StubCommand {
            CanExecute = ICommandExtensions.CanAlwaysExecute,
            Execute = arg => SelectedIndex = selectedIndex == 2 ? -1 : 2,
         };

         hideCommand = new StubCommand {
            CanExecute = arg => SelectedIndex != -1,
            Execute = arg => SelectedIndex = -1,
         };

         SelectedIndex = -1;
      }

      public void Schedule(Action action) {
         if (currentDeferralToken != null) {
            deferredWork.Add(action);
         } else {
            action();
         }
      }

      public IEnumerator<IToolViewModel> GetEnumerator() => tools.GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
   }
}
