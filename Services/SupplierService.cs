using ConstruxERP.Models;
using ConstruxERP.Repositories;
using System.Collections.Generic;

namespace ConstruxERP.Services
{
    public class SupplierService
    {
        private readonly SupplierRepository _repo = new();

        public List<Supplier> GetAll() => _repo.GetAll();

        public void AddSupplier(Supplier s)
        {
            if (string.IsNullOrWhiteSpace(s.Name)) return;
            _repo.Insert(s);
        }
    }
}