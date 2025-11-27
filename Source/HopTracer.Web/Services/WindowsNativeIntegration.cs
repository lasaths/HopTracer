using System.Windows.Forms;
using HopTracer.Core.Services;

namespace HopTracer.Web.Services;

public class WindowsNativeIntegration : INativeIntegration
{
    public Task<string?> PickFileAsync(string title)
    {
        var tcs = new TaskCompletionSource<string?>();

        var thread = new Thread(() =>
        {
            try
            {
                // Create a hidden, top-most form to act as the owner
                // This ensures the dialog appears in front of the browser
                using var owner = new Form
                {
                    TopMost = true,
                    Opacity = 0,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.CenterScreen,
                    WindowState = FormWindowState.Minimized
                };
                
                // We must show the form to create its handle
                owner.Show();

                using var dialog = new OpenFileDialog();
                dialog.Title = title;
                dialog.Filter = "Grasshopper Files (*.gh;*.ghx)|*.gh;*.ghx|All files (*.*)|*.*";
                dialog.RestoreDirectory = true;
                dialog.AutoUpgradeEnabled = true;

                if (dialog.ShowDialog(owner) == DialogResult.OK)
                {
                    tcs.SetResult(dialog.FileName);
                }
                else
                {
                    tcs.SetResult(null);
                }
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return tcs.Task;
    }
}
