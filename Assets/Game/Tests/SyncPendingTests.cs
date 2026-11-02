using System.Linq;
using NUnit.Framework;
using RobotBombero.Simulation;
using UnityEngine.UIElements;

namespace RobotBombero.Game.Tests
{
    /// <summary>Borrados y cambios de packs que deben acabar llegando al servidor aunque falle la conexión.</summary>
    public class SyncPendingTests : UiFixture
    {
        private LocalPack EditedPack(string id, long serverId = 55)
        {
            LocalPack pack = AddPack(id, "Pack " + id, serverId);
            pack.content.rules[0].battery = "Low";
            Context.Packs.Save(pack);
            return pack;
        }

        [Test]
        public void SyncPending_RetriesEditsAndDeletes()
        {
            Start();
            LocalPack edited = EditedPack("p1", 55);
            LocalPack gone = AddPack("p2", "Otro", 56);
            Api.DeleteResult = ApiResult<bool>.ConnectionFailed("sin red");
            Context.Packs.Delete(gone);
            Assert.AreEqual(new long[] { 56 }, Context.Packs.PendingDeletes.ToArray());
            Api.DeleteResult = ApiResult<bool>.Ok(true);

            Drain(Context.Packs.SyncPending());

            Assert.AreEqual(1, Api.DeleteCalls);
            Assert.AreEqual(56, Api.LastDeletedPackId);
            Assert.AreEqual(1, Api.SavePackCalls);
            Assert.IsFalse(Context.Packs.NeedsSync(edited));
            Assert.AreEqual(0, Context.Packs.PendingDeletes.Count);
            Assert.AreEqual(0, new LocalStore(StoreRoot).LoadPendingDeletes().Count, "También en disco");
        }

        [Test]
        public void SyncPending_FailureStaysPending()
        {
            Start();
            LocalPack edited = EditedPack("p1", 55);
            Context.Packs.Delete(AddPack("p2", "Otro", 56));
            Api.DeleteResult = ApiResult<bool>.ConnectionFailed("sin red");
            Api.SavePackResult = ApiResult<long>.ConnectionFailed("sin red");

            Drain(Context.Packs.SyncPending());

            Assert.AreEqual(new long[] { 56 }, Context.Packs.PendingDeletes.ToArray());
            Assert.AreEqual(new long[] { 56 }, new LocalStore(StoreRoot).LoadPendingDeletes().ToArray());
            Assert.IsTrue(Context.Packs.NeedsSync(edited));
            Assert.IsTrue(Context.HasIdentity, "Sin conexión no es una sesión caducada");
            Assert.AreEqual(0, Api.SavePackCalls, "Sin conexión no insiste con cada pack");
        }

        [Test]
        public void DeletePack_Offline_QueuedAndRetried()
        {
            Start();
            AddPack("p1", "Alpha", 55);
            Api.DeleteResult = ApiResult<bool>.ConnectionFailed("sin red");
            Router.Show(ScreenId.Rules);

            Click("pack-delete-0");
            Click("dialog-ok");

            Assert.AreEqual(0, Store.LoadPacks().Count);
            Assert.AreEqual(new long[] { 55 }, new LocalStore(StoreRoot).LoadPendingDeletes().ToArray(), "Sigue anotado para reintentarlo");

            Api.DeleteResult = ApiResult<bool>.Ok(true);
            Router.Show(ScreenId.Rules);

            Assert.AreEqual(2, Api.DeleteCalls, "Al entrar en Reglas se reintenta");
            Assert.AreEqual(0, new LocalStore(StoreRoot).LoadPendingDeletes().Count);
        }

        [Test]
        public void DeletePack_NotFoundOnServer_ClearsPending()
        {
            Start();
            Context.Packs.Delete(AddPack("p1", "Alpha", 55));
            Api.DeleteResult = ApiResult<bool>.Rejected(404, "No existe.");

            Drain(Context.Packs.SyncPending());

            Assert.AreEqual(0, Context.Packs.PendingDeletes.Count);
        }

        [Test]
        public void DeletePack_NeverUploaded_QueuesNothing()
        {
            Start();

            Context.Packs.Delete(AddPack("p1", "Alpha", 0));

            Assert.AreEqual(0, Context.Packs.PendingDeletes.Count);
            Assert.AreEqual(0, new LocalStore(StoreRoot).LoadPendingDeletes().Count);
        }

        [Test]
        public void SyncInFlight_DeletedPack_NotRestored()
        {
            Start();
            LocalPack pack = EditedPack("p1", 55);
            Api.OnSavePack = () => Context.Packs.Delete(pack);

            Drain(Context.Packs.Sync(pack));

            Assert.AreEqual(0, Store.LoadPacks().Count, "El archivo del pack no debe volver");
        }

        [Test]
        public void SyncPending_ConcurrentRun_DoesNotRepeatRequests()
        {
            Start();
            Context.Packs.Delete(AddPack("p1", "Alpha", 55));
            Api.DeleteStalls = true;

            var stack = new System.Collections.Generic.Stack<System.Collections.IEnumerator>();
            stack.Push(Context.Packs.SyncPending());
            // Avanza hasta el primer fotograma cedido de verdad (la petición detenida).
            Assert.IsTrue(Step(stack));
            var second = Context.Packs.SyncPending();
            Assert.IsFalse(second.MoveNext(), "Ya hay una pasada en marcha");
            Api.DeleteStalls = false;
            while (Step(stack)) { }

            Assert.AreEqual(1, Api.DeleteCalls);
            Assert.AreEqual(0, Context.Packs.PendingDeletes.Count);
        }

        private static bool Step(System.Collections.Generic.Stack<System.Collections.IEnumerator> stack)
        {
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is System.Collections.IEnumerator nested) { stack.Push(nested); continue; }
                return true;
            }
            return false;
        }

        [Test]
        public void SyncPending_Unauthorized_EndsSession_AndForgetsPendingDeletes()
        {
            Start();
            Context.Packs.Delete(AddPack("p1", "Alpha", 55));
            Api.DeleteResult = ApiResult<bool>.Rejected(401, "Clave no válida.");

            Drain(Context.Packs.SyncPending());

            Assert.AreEqual(ScreenId.Login, Router.Current);
            Assert.IsFalse(Context.HasIdentity);
            Assert.AreEqual(0, new LocalStore(StoreRoot).LoadPendingDeletes().Count);
        }

        [Test]
        public void AfterLogin_PendingDeletesAreSent()
        {
            Store.SavePendingDeletes(new System.Collections.Generic.List<long> { 77 });
            Start(false);
            Api.CreatePlayerResult = ApiResult<PlayerCreated>.Ok(new PlayerCreated { playerId = 8, name = "Ana", key = "k-secret" }, 201);

            Root.Q<TextField>("login-name").value = "Ana";
            Click("login-enter");

            Assert.AreEqual(ScreenId.Menu, Router.Current);
            Assert.AreEqual(1, Api.DeleteCalls);
            Assert.AreEqual(77, Api.LastDeletedPackId);
        }
    }
}
