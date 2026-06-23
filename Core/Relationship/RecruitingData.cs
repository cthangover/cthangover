using System;
using System.Collections.Generic;
using Cthangover.Core.Settings;

namespace Cthangover.Core.Relationship
{
    
    public class RecruitingData
    {
        public List<Recruit> Data { get; set; } = new();
        
        public bool HasID(string id)
        {
            return GetByID(id) != null;
        }
        
        public Recruit GetByID(CharacterType type)
        {
            return GetByID(type.ToString());
        }
        
        public Recruit GetByID(string id)
        {
            foreach (var item in Data)
            {
                if (item.ID == id)
                    return item;
            }
            return null;
        }

        private string GenID(string id)
        {
            for (;;)
            {
                if (!HasID(id))
                    return id;
                id = Guid.NewGuid().ToString();
            }
        }

        public Recruit Add(string id, string characterID)
        {
            var data = new Recruit
            {
                ID = GenID(id),
                CharacterID = characterID,
            };
            data.Properties.SetInt(Recruit.PROP_HEALTH, 10);
            Data.Add(data);
            
            RecruitBehaviourRegistry.Instance.OnConfigure(data);
            return data;
        }

        public void Remove(string id)
        {
            for (int i = Data.Count - 1; i >= 0; i--)
            {
                if (Data[i].ID == id)
                {
                    RecruitBehaviourRegistry.Instance.OnRemove(Data[i]);
                    Data.RemoveAt(i);
                    return;
                }
            }
        }
        
    }

}
