﻿using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class DiffTests {
      private readonly EditorViewModel editor;

      public DiffTests() {
         editor = new EditorViewModel(new StubFileSystem(), InstantDispatch.Instance);
         editor.Open.Execute(new LoadedFile("Left.gba", new byte[0x200]));
         editor.Open.Execute(new LoadedFile("Right.gba", new byte[0x200]));
         ViewModel0.Width = 16;
         ViewModel1.Width = 16;
         ViewModel0.Height = 16;
         ViewModel1.Height = 16;
      }

      private IEditableViewPort ViewModel0 => (IEditableViewPort)editor[0];
      private IEditableViewPort ViewModel1 => (IEditableViewPort)editor[1];
      private IViewPort ViewModel2 => (IViewPort)editor[2];
      private IDataModel Model0 => ViewModel0.Model;
      private IDataModel Model1 => ViewModel1.Model;
      private void Edit0(string text) => ((ViewPort)editor[0]).Edit(text);
      private void Edit1(string text) => ((ViewPort)editor[1]).Edit(text);

      [Fact]
      public void TwoTabs_Diff_NameIsFromBothTabs() {
         Model1[0] = 1;

         editor.DiffRight.Execute(editor[0]);

         Assert.Equal("Left -> Right", editor[2].Name);
      }

      [Fact]
      public void TwoTabsWithSameData_Diff_NoNewTab() {
         editor.DiffRight.Execute(editor[0]);
         Assert.Equal(2, editor.Count);
      }

      [Fact]
      public void TwoTabs_Diff_FullWidthPlusOne() {
         Model1[0] = 1;

         editor.DiffRight.Execute(editor[0]);

         Assert.Equal(16 + 1 + 16, ((IViewPort)editor[2]).Width);
      }

      [Fact]
      public void TwoTabs_Diff_DiffBytesAreSelected() {
         Model1[0] = 1;

         editor.DiffRight.Execute(editor[0]);

         Assert.True(ViewModel2.IsSelected(new Point(17, 0)));
      }

      [Fact]
      public void TwoTabs_DifferentFormatsForDiff_AlignmentStillMatches() {
         Edit1("^table[a: b: c:]4 1 2 3 4 5 6 7 8 9 10 11 12 ");
         Model1[0x100] = 1;

         editor.DiffRight.Execute(editor[0]);

         Assert.All(10.Range(), y => {
            var leftIsUndefined = ViewModel2[0, y].Format == Undefined.Instance;
            var rightIsUndefined = ViewModel2[17, y].Format == Undefined.Instance;
            Assert.Equal(leftIsUndefined, rightIsUndefined);
         });
      }

      [Fact]
      public void TwoTabs_TwoDifferences_SeeBothDifferences() {
         Model1[0x000] = 10;
         Model1[0x100] = 20;

         editor.DiffRight.Execute(editor[0]);

         Assert.IsNotType<Undefined>(ViewModel2[0, 6].Format);
      }

      [Fact]
      public void DiffTab_PageDown_MoveDownOnePage() {
         foreach (var address in 7.Range().Select(i => i * 0x50))
            Model1[address] = 1;
         editor.DiffRight.Execute(editor[0]);

         ViewModel2.Scroll.Execute(Direction.PageDown);

         Assert.Equal(16, ViewModel2.ScrollValue);
      }

      [Fact]
      public void LeftMostTab_DiffLeft_CanNotExecute() {
         Assert.False(editor.DiffLeft.CanExecute(ViewModel0));
      }

      [Fact]
      public void RightMostTab_DiffRight_CanNotExecute() {
         Assert.False(editor.DiffRight.CanExecute(ViewModel1));
      }

      [Fact]
      public void LeftMostTab_ExecuteDiffLeft_NoTabAdded() {
         editor.DiffLeft.Execute(ViewModel0);
         Assert.Equal(2, editor.Count);
      }

      [Fact]
      public void RightMostTab_ExecuteDiffRight_NoTabAdded() {
         editor.DiffRight.Execute(ViewModel1);
         Assert.Equal(2, editor.Count);
      }

      [Fact]
      public void NonViewPortTab_TryDiffFromNonViewPort_NoDiff() {
         editor.Add(new StubTabContent());

         editor.DiffLeft.Execute(editor[2]);

         Assert.False(editor.DiffLeft.CanExecute(editor[2]));
         Assert.Equal(3, editor.Count);
      }

      [Fact]
      public void NonViewPortTab_TryDiffToNonViewPort_NoDiff() {
         editor.Add(new StubTabContent());

         editor.DiffRight.Execute(editor[1]);

         Assert.False(editor.DiffRight.CanExecute(editor[1]));
         Assert.Equal(3, editor.Count);
      }

      [Fact]
      public void DiffTab_SaveAll_NoCrash() {
         Model1[1] = 1;
         ViewModel0.DiffRight.Execute();

         editor.SaveAll.Execute();
      }

      [Fact]
      public void DiffTab_Close_Closes() {
         Model1[1] = 1;
         ViewModel0.DiffRight.Execute();

         editor.Close.Execute();

         Assert.Equal(2, editor.Count);
      }

      [Theory]
      [InlineData(2, 2, 0, 34)]
      [InlineData(19, 2, 1, 34)]
      public void DiffTab_ExpandSelection_GotoInNewTab(int x, int y, int targetTab, int newSelectionStart) {
         Model1[1] = 1;
         ViewModel0.DiffRight.Execute();

         ViewModel2.ExpandSelection(x, y);

         Assert.Equal(targetTab, editor.SelectedIndex);
         var selectedTab = (ViewPort)editor.SelectedTab;
         var selectedAddress = selectedTab.ConvertViewPointToAddress(selectedTab.SelectionStart);
         Assert.Equal(newSelectionStart, selectedAddress);
      }

      [Fact]
      public void ManyChanges_Diff_LimitTo1000Changes() {
         editor.MaximumDiffSegments = 2;
         Model1[1] = 1;
         Model1[30] = 1;
         Model1[60] = 1;

         ViewModel0.DiffRight.Execute();

         Assert.Contains("+", editor.InformationMessage);
      }

      [Fact]
      public void WideViewPorts_Diff_LimitTotalWidth() {
         ViewModel0.Edit("^table[a1:: a2:: a3:: a4:: b1:: b2:: b3:: b4:: c1:: c2:: c3:: c4::]2 ");
         ViewModel1.Edit("^table[a1:: a2:: a3:: a4:: b1:: b2:: b3:: b4:: c1:: c2:: c3:: c4::]2 ");
         Model1[1] = 1;

         ViewModel0.DiffRight.Execute();

         Assert.Equal(32 + 1 + 32, ViewModel2.Width);
      }

      [Fact]
      public void DifferentLengthFiles_Diff_Error() {
         Model1.ExpandData(new ModelDelta(), 0x300);

         ViewModel0.DiffRight.Execute();

         Assert.True(editor.ShowError);
      }

      [Fact]
      public void MultipleCloseChanges_Diff_MultipleSections() {
         for (int i = 0; i < 0x10 * 5; i++) Model1[i] = 1;

         ViewModel0.DiffRight.Execute();

         Assert.All(Enumerable.Range(0, 5),
            i => Assert.IsNotType<Undefined>(ViewModel2[0, i].Format));
      }

      [Fact]
      public void Diff_ContextMenu_HasCopyAddress() {
         Model1[1] = 1;
         ViewModel0.DiffRight.Execute();

         var items = ViewModel2.GetContextMenuItems(new Point(0, 0));
         var itemText = items.Select(item => item.Text).ToList();

         Assert.Single(itemText, "Copy Address");
      }
   }
}
