using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TSARScanner
{
    [Activity(Label = "MtrActivity", Theme = "@android:style/Theme.Material.NoActionBar", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class MtrActivity : Activity
    {
        private RecyclerView displayLinkedMTRs = null;
        private TextView emptyView = null;
        private AutoCompleteTextView textViewAutoComplete = null;

        // Adapter that accesses the data set (MTRs):
        private RecyclerAdapter adapterLinkedMTRs = null;
        private AutoAdapter autoAdapter = null;

        private readonly Utilities util = new Utilities();

        MTRrecord mtrSelected = new MTRrecord();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Layout manager that lays out each card in the RecyclerView:
            RecyclerView.LayoutManager myLayoutManager;

            List<MTRrecord> allMTRs = new List<MTRrecord>();

            SetContentView(Resource.Layout.mtr_activity);
            displayLinkedMTRs = FindViewById<RecyclerView>(Resource.Id.recyclerView);
            emptyView = FindViewById<TextView>(Resource.Id.emptyView);
            textViewAutoComplete = FindViewById<AutoCompleteTextView>(Resource.Id.autocomplete_mtr);

            textViewAutoComplete.InputType = 0; // This removes the soft keyboard from MtrActivity

            // Use the built-in linear layout manager:
            myLayoutManager = new LinearLayoutManager(this);
            displayLinkedMTRs.SetLayoutManager(myLayoutManager);
            allMTRs = util.GetMTRs();

            // Create an adapter for the AutoCompleteTextView and pass it the data set (all MTRs) to manage
            autoAdapter = new AutoAdapter(this, allMTRs);
            textViewAutoComplete.Adapter = autoAdapter;

            // Create an adapter for the RecyclerView, and pass it the
            // data set (the MTRs) to manage:
            adapterLinkedMTRs = new RecyclerAdapter(Global.LinkedMTRs);

            // Plug the adapter into the RecyclerView:
            displayLinkedMTRs.SetAdapter(adapterLinkedMTRs);

            // Register the item click handler (below) with the adapter:
            adapterLinkedMTRs.ItemClick += OnItemLongClick;

            textViewAutoComplete.SetDropDownBackgroundDrawable(GetDrawable(Resource.Drawable.abc_popup_background_mtrl_mult));
            textViewAutoComplete.ItemClick += AutoCompleteTextView_ItemClick;

            util.CheckIfEmpty(displayLinkedMTRs, emptyView);
        }

        private void OnItemLongClick(object sender, int e)
        {
            // Assign the clicked MTR to mtrSelected
            mtrSelected = Global.LinkedMTRs[e];

            var dlgAlert = (new AlertDialog.Builder(this)).Create();
            dlgAlert.SetMessage("Are you sure you want to remove the selected MTR?");
            dlgAlert.SetButton("Remove", HandlerDialogButton);
            dlgAlert.SetButton2("Cancel", HandlerDialogButton);
            dlgAlert.Show();
        }

        private async void HandlerDialogButton(object sender, DialogClickEventArgs e)
        {
            AlertDialog alertDialog = sender as AlertDialog;
            Button btnClicked = alertDialog.GetButton(e.Which);
            if (btnClicked.Text == "Remove")
            {
                Global.LinkedMTRs.Remove(mtrSelected);
                util.CheckIfEmpty(displayLinkedMTRs, emptyView);
                //await Task.Delay(1);
                await Task.Run(() => util.RemoveLink(mtrSelected.LinkedId, mtrSelected.Id));
            }
            else
                alertDialog.Dispose();

            adapterLinkedMTRs.NotifyDataSetChanged();
        }

        // Event handler for when an item from the autocompletetextview is clicked
        private async void AutoCompleteTextView_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            MTRrecord mtr = new MTRrecord();
            mtr = (MTRrecord)autoAdapter.GetItem(e.Position);
            textViewAutoComplete.Text = "";
            //holder = displayLinkedMTRs.FindViewHolderForAdapterPosition(e.Position);

            try
            {
                //await Task.Delay(1);
                Global.LinkedMTRs.Add(mtr);
                Global.LinkedMTRs.OrderBy(x => x.Name).ToList();
                mtr.LinkedId = await Task.Run(() => util.LinkMTRToDeal(mtr.Id));
            }
            catch (Exception ex)
            {
                var dlgAlert = (new AlertDialog.Builder(this)).Create();
                dlgAlert.SetMessage((Java.Lang.ICharSequence)ex);
                dlgAlert.SetButton("Close", HandlerDialogButtonAuto);
                dlgAlert.Show();
            }

            util.CheckIfEmpty(displayLinkedMTRs, emptyView);
            adapterLinkedMTRs.NotifyDataSetChanged();
        }

        public void HandlerDialogButtonAuto(object sender, DialogClickEventArgs e)
        {
            AlertDialog alertDialog = sender as AlertDialog;
            Button btnClicked = alertDialog.GetButton(e.Which);
            if (btnClicked.Text == "Close")
            {
                alertDialog.Dispose();
            }
            else
                alertDialog.Dispose();
        }
        protected override void OnResume()
        {
            base.OnResume();
            BarcodeScanner.DisableScanner();
        }
    }

    public class MTRViewHolder : RecyclerView.ViewHolder
    {
        public TextView MtrName { get; private set; }
        public TextView MtrThickness { get; private set; }
        public TextView MtrSize { get; private set; }
        public TextView MtrGrade { get; private set; }

        // Get references to the views defined in the CardView layout.
        public MTRViewHolder(View itemView, Action<int> listener)
            : base(itemView)
        {
            // Locate and cache view references:
            MtrName = itemView.FindViewById<TextView>(Resource.Id.mtrName);
            MtrThickness = itemView.FindViewById<TextView>(Resource.Id.mtrThickness);
            MtrSize = itemView.FindViewById<TextView>(Resource.Id.mtrSize);
            MtrGrade = itemView.FindViewById<TextView>(Resource.Id.mtrGrade);

            // Detect user clicks on the item view and report which item
            // was clicked (by layout position) to the listener:
            itemView.LongClick += (sender, e) => listener(base.LayoutPosition);
        }
    }

    //----------------------------------------------------------------------
    // ADAPTER

    // Adapter to connect the data set (MTR record) to the RecyclerView: 
    public class RecyclerAdapter : RecyclerView.Adapter
    {
        // Event handler for item clicks:
        public event EventHandler<int> ItemClick;

        // Underlying data set (MTRs):
        public List<MTRrecord> myMTRrecord;

        // Load the adapter with the data set (MTR record) at construction time:
        public RecyclerAdapter(List<MTRrecord> MTR_record)
        {
            myMTRrecord = MTR_record;
        }
        // Create a new photo CardView (invoked by the layout manager): 
        public override RecyclerView.ViewHolder
            OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            // Inflate the CardView for the photo:
            View itemView = LayoutInflater.From(parent.Context).
                        Inflate(Resource.Layout.recycler_layout, parent, false);

            // Create a ViewHolder to find and hold these view references, and 
            // register OnClick with the view holder:
            MTRViewHolder vh = new MTRViewHolder(itemView, OnClick);
            return vh;
        }

        // Fill in the contents of the MTR view (invoked by the layout manager):
        public override void
            OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            MTRViewHolder vh = holder as MTRViewHolder;

            // Set the TextViews in this ViewHolder's CardView 
            // from this position in the MTRs:
            vh.MtrName.Text = myMTRrecord[position].Name;
            vh.MtrThickness.Text = myMTRrecord[position].Thickness;
            vh.MtrSize.Text = myMTRrecord[position].Size;
            vh.MtrGrade.Text = myMTRrecord[position].Grade;
        }

        // Return the number of MTRs available:
        public override int ItemCount
        {
            get { return myMTRrecord.Count; }
        }


        // Raise an event when the item-click takes place:
        void OnClick(int position)
        {
            if (ItemClick != null)
                ItemClick(this, position);
        }
    }

    public class AutoAdapter : BaseAdapter<MTRrecord>, IFilterable
    {
        List<MTRrecord> originalData;
        List<MTRrecord> mtrItems;
        private readonly Activity context;

        public AutoAdapter(Activity activity, List<MTRrecord> mtrs)
            : base()
        {
            mtrItems = mtrs.OrderBy(x => x.Name).ToList();
            context = activity;
            Filter = new MTRFilter(this);
        }

        public override long GetItemId(int position)
        {
            return position;
        }

        public override MTRrecord this[int position]
        {
            get { return mtrItems[position]; }
        }

        public override int Count
        {
            get { return mtrItems.Count; }
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            var view = LayoutInflater.From(parent.Context).
                        Inflate(Resource.Layout.mtr_autocomplete_row, parent, false);

            var mtr = mtrItems[position];

            var nameView = view.FindViewById<TextView>(Resource.Id.mtrAutoName);
            var thicknessView = view.FindViewById<TextView>(Resource.Id.mtrAutoThickness);
            var sizeView = view.FindViewById<TextView>(Resource.Id.mtrAutoSize);
            var gradeView = view.FindViewById<TextView>(Resource.Id.mtrAutoGrade);

            nameView.Text = mtr.Name;
            thicknessView.Text = mtr.Thickness;
            sizeView.Text = mtr.Size;
            gradeView.Text = mtr.Grade;

            return view;
        }

        public Filter Filter { get; private set; }

        public override void NotifyDataSetChanged()
        {
            // Possibly update indices here
            base.NotifyDataSetChanged();
        }

        private class MTRFilter : Filter
        {
            private readonly AutoAdapter adapter;
            public MTRFilter(AutoAdapter _adapter)
            {
                adapter = _adapter;
            }

            protected override FilterResults PerformFiltering(Java.Lang.ICharSequence constraint)
            {
                var returnObj = new FilterResults();
                var results = new List<MTRrecord>();

                if (adapter.originalData == null)
                    adapter.originalData = adapter.mtrItems;

                if (constraint == null) return returnObj;

                if (adapter.originalData != null && adapter.originalData.Any())
                {
                    // Compare constraint to all names lowercased. 
                    // It they are contained they are added to results.
                    results.AddRange(
                        adapter.originalData.Where(
                            mtrRecord => mtrRecord.Name.ToLower().Contains(constraint.ToString())));
                }

                var resultsNoDuplicates = results.Where(x => Global.LinkedMTRs.All(y => y.Id != x.Id)).ToList(); // Uses the Id property to check for duplicates

                // This is a potentially faster way to filter duplicates if runspeed becomes an issue, but this code is currently incomplete and does not work properly
                // var resultsNoDuplicates = (from result in results join mtr in Global.LinkedMTRs on result.Id equals mtr.Id into matches where matches.Any() select result).ToList();

                // Nasty piece of .NET to Java wrapping, be careful with this!
                returnObj.Values = resultsNoDuplicates.ToArray();
                returnObj.Count = resultsNoDuplicates.Count;

                constraint.Dispose();

                return returnObj;
            }

            protected override void PublishResults(Java.Lang.ICharSequence constraint, FilterResults results)
            {

                // This whole thing needs to be in the if statement because if constraint is null then values will be null and it will have a null exception
                if (constraint != null)
                {
                    using (var values = results.Values)
                        adapter.mtrItems = new List<MTRrecord>(values.ToArray<MTRrecord>());

                    adapter.NotifyDataSetChanged();

                    // Don't do this and see GREF counts rising
                    constraint.Dispose();
                }  
                
                results.Dispose();
            }
        }
    }

}