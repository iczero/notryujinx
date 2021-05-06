using Ryujinx.Ava.Ui.Controls;
using Ryujinx.Ava.Ui.Windows;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using UserProfile = Ryujinx.Ava.Ui.Models.UserProfile;

namespace Ryujinx.Ava.Ui.ViewModels
{
    public class UserProfileViewModel : BaseModel, IDisposable
    {
        private const uint MaxProfileNameLength = 0x20;

        private readonly UserProfileWindow _owner;

        private UserProfile _selectedProfile;
        private string _tempUserName;

        public UserProfileViewModel()
        {
            Profiles = new ObservableCollection<UserProfile>();
        }

        public UserProfileViewModel(UserProfileWindow owner) : this()
        {
            _owner = owner;

            LoadProfiles();
        }

        public ObservableCollection<UserProfile> Profiles { get; set; }

        public UserProfile SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                _selectedProfile = value;

                OnPropertyChanged();
            }
        }

        public void Dispose()
        {
        }

        public async void LoadProfiles()
        {
            Profiles.Clear();

            IOrderedEnumerable<HLE.HOS.Services.Account.Acc.UserProfile> profiles = _owner.AccountManager.GetAllUsers()
                .OrderByDescending(x => x.AccountState == AccountState.Open);

            foreach (HLE.HOS.Services.Account.Acc.UserProfile profile in profiles)
            {
                Profiles.Add(new UserProfile(profile));
            }

            SelectedProfile = Profiles.First(x => x.UserId == _owner.AccountManager.LastOpenedUser.UserId);
        }

        public async void ChooseProfileImage()
        {
            await SelectProfileImage();
        }

        public async Task SelectProfileImage(bool isNewUser = false)
        {
            ProfileImageSelectionDialog selectionDialog = new(_owner.ContentManager);

            await selectionDialog.ShowDialog(_owner);

            if (selectionDialog.BufferImageProfile != null)
            {
                if (isNewUser)
                {
                    if (!string.IsNullOrWhiteSpace(_tempUserName))
                    {
                        _owner.AccountManager.AddUser(_tempUserName, selectionDialog.BufferImageProfile);
                    }
                }
                else if (SelectedProfile != null)
                {
                    _owner.AccountManager.SetUserImage(SelectedProfile.UserId, selectionDialog.BufferImageProfile);
                    SelectedProfile.Image = selectionDialog.BufferImageProfile;

                    SelectedProfile = null;
                }

                LoadProfiles();
            }
        }

        public async void AddUser()
        {
            _tempUserName = await AvaDialog.CreateInputDialog("Choose the Profile Name", "Please Enter a Profile Name",
                $"(Max Length : {MaxProfileNameLength})", _owner, MaxProfileNameLength);

            if (!string.IsNullOrWhiteSpace(_tempUserName))
            {
                await SelectProfileImage(true);
            }

            _tempUserName = String.Empty;
        }

        public async void DeleteUser()
        {
            if (_selectedProfile != null)
            {
                _owner.AccountManager.DeleteUser(_selectedProfile.UserId);
            }

            LoadProfiles();
        }
    }
}