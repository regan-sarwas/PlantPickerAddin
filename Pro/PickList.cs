﻿using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlantPickerAddin
{
    class PickList
    {
        public PickList(string fgdb, string table, string field)
        {
            _fgdbFileName = fgdb;
            _pickListTableName = table;
            _pickListFieldName = field;
        }
        private readonly string _fgdbFileName;
        private readonly string _pickListTableName;
        private readonly string _pickListFieldName;
        private string[] _names = new string[0];
        private bool _loaded = false;

        /// <summary>
        /// An enumeration of text values for use in a picklist (combo box).
        /// The enumeration is never null, but it will be empty if the picklist has not been loaded (or failed to load). 
        /// </summary>
        public IEnumerable<string> Names
        {
            get
            {
                return _names;
            }
        }

        /// <summary>
        /// Creates an asynchronous queued task to populate <see cref="Names"/> with the list of text values from the field,
        /// table, and FGDB set in the constructor.
        /// Can be called multiple times, but it will return cached values unless the previous call failed.
        /// 
        /// Will generate any number of exceptions.  Anything but a <see cref="ConfigurationException"/> should be a programmming error.
        /// A ConfigurationException will result if the necessary external data for the picklist is deleted, moved, or altered.
        /// </summary>
        public async Task LoadAsync()
        {
            await QueuedTask.Run(() => Load());
        }

        private void Load()
        {
            if (_loaded) { return; }
            try
            {
                _names = GetNames().ToArray();
                _loaded = true;
            }
            catch (GeodatabaseNotFoundOrOpenedException exception)
            {
                throw new ConfigurationException($"Picklist database ({_fgdbFileName}) not found: {exception.Message}.");
            }
            catch (GeodatabaseTableException)
            {
                throw new ConfigurationException($"Picklist table ({_pickListTableName}) was not found.");
            }
        }

        /// <summary>
        /// Reads the list of text values from the field, table, and FGDB set in the constructor.
        /// Must be run on the MCT; Call within <see cref="ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run"/>
        /// </summary>
        /// <returns>A list of strings to use as picklist values.</returns>
        private List<string> GetNames()
        {
            var names = new List<string>();

            using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(_fgdbFileName))))
            // GeodatabaseNotFoundOrOpenedException if _fgdbFileName not found or can't be opened.
            {
                using (Table table = geodatabase.OpenDataset<Table>(_pickListTableName))
                //GeodatabaseCatalogDatasetException if name not found
                {
                    TableDefinition tableDefinition = table.GetDefinition();
                    var index = tableDefinition.FindField(_pickListFieldName);
                    if (index < 0)
                    {
                        throw new ConfigurationException($"Picklist field ({_pickListFieldName}) not found.");
                    }
                    if (tableDefinition.GetFields()[index].FieldType != FieldType.String)
                    {
                        throw new ConfigurationException($"Picklist field ({_pickListFieldName}) is not a text field.");
                    }
                    using (RowCursor rowCursor = table.Search(null, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            string name = (string)rowCursor.Current[index];
                            names.Add(name);
                        }
                    }
                }
            }
            return names;
        }
    }
}
